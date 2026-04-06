using System.Text.Json;
using System.Text.RegularExpressions;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Agents;

/// <summary>
/// CEO Agent 核心邏輯：組建 Prompt、呼叫 LLM 或 Claude Code、解析 JSON 回應。
/// Stage 9：加入智慧分類（Bug / 新功能 / 正常行為 / 疑問）、提案模式（propose action）。
/// Stage 15：Victoria 接上 Claude Code（ProcessWithClaudeCodeAsync）、Session 對話歷史、長期記憶。
/// </summary>
public class CeoAgentService(
    LlmProviderFactory providerFactory,
    TaskRepository taskRepository,
    GitHubService gitHubService,
    ClaudeCodeService claudeCodeService,
    CeoConversationRepository conversationRepository,
    CeoMemoryRepository memoryRepository,
    IOptions<GitHubSettings> gitHubSettings,
    IConfiguration configuration,
    ILogger<CeoAgentService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>確保 CLAUDE.md swap 的序列化，避免同時觸發兩個 Victoria Claude Code session 時互相覆寫。</summary>
    private static readonly SemaphoreSlim VictoriaLock = new(1, 1);

    private readonly GitHubSettings _github = gitHubSettings.Value;

    /// <summary>
    /// 處理使用者輸入，回傳 CEO 的分析結果。
    /// 可選傳入圖片附件（如 Discord 截圖）與對話歷史（多輪自然語言對話用）。
    /// </summary>
    public async Task<CeoResponse> ProcessAsync(
        string userInput,
        string projectName,
        IReadOnlyList<AgentDescriptor> agentList,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null,
        IReadOnlyList<ConversationTurn>? history = null,
        IReadOnlyList<string>? availableProjects = null)
    {
        var provider = providerFactory.Create("CEO");

        var systemPrompt = BuildSystemPrompt(agentList, rules);
        var userMessage  = await BuildUserMessageAsync(userInput, projectName, history, cancellationToken, availableProjects);

        // 最多重試一次（回應格式錯誤時）
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken, images);

            var parsed = TryParseResponse(response.Content);
            if (parsed is not null)
            {
                logger.LogInformation(
                    "CEO 回應解析成功（第 {Attempt} 次），action={Action} target={Agent} require_confirmation={Confirm} InputTokens={Input} OutputTokens={Output}",
                    attempt, parsed.Action, parsed.TargetAgent, parsed.RequireConfirmation, response.InputTokens, response.OutputTokens);
                return parsed;
            }

            logger.LogWarning("CEO 回應格式錯誤（第 {Attempt} 次），原始內容：{Content}", attempt, response.Content);
        }

        // 兩次都失敗，回傳通知訊息
        return new CeoResponse { Reply = "CEO 回應格式錯誤，請查看 log 或稍後再試。" };
    }

    /// <summary>
    /// Stage 15：Victoria CEO 的主要處理路徑（Claude Code 模式）。
    /// 可探索 codebase、讀寫 docs/、git commit + push，同時維護 Session 對話歷史與長期記憶。
    /// 若 CloneOrPull 失敗或 repo 未設定，自動降級為直接 LLM 呼叫（ProcessAsync）。
    /// </summary>
    public async Task<CeoResponse> ProcessWithClaudeCodeAsync(
        string userInput,
        string userId,
        string projectName,
        IReadOnlyList<AgentDescriptor> agentList,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null,
        IReadOnlyList<string>? availableProjects = null)
    {
        // ── 1. Session 解析 ──────────────────────────────────────────────
        var sessionId = await conversationRepository.GetActiveSessionIdAsync(userId, cancellationToken);

        // ── 2. 載入對話歷史（最近 20 筆） ───────────────────────────────
        var historyTurns = await conversationRepository.GetSessionHistoryAsync(sessionId, cancellationToken);

        // ── 3. 載入長期記憶（最多 100 筆） ──────────────────────────────
        var memories = await memoryRepository.GetActiveMemoriesAsync(userId, cancellationToken);

        // ── 4. 取得 repo 副本（與其他 Agent 一致的 CloneOrPull 機制）────
        // 容器內無法存取 Windows host 路徑，必須透過 CloneOrPull 取得本地副本。
        var repoName = !string.IsNullOrWhiteSpace(projectName) ? projectName : _github.DefaultRepo;
        string? repoPath = null;
        string? fallbackReason = null;   // 降級原因，會附在 Discord 回應供診斷

        if (!string.IsNullOrWhiteSpace(repoName) && !string.IsNullOrWhiteSpace(_github.Owner))
        {
            try
            {
                repoPath = gitHubService.CloneOrPull(_github.Owner, repoName, "victoria");
                logger.LogInformation("Victoria repo 準備完成（path={Path}）", repoPath);
            }
            catch (Exception ex)
            {
                fallbackReason = $"CloneOrPull 失敗：{ex.Message}";
                logger.LogWarning(ex, "Victoria CloneOrPull 失敗，降級使用直接 LLM 模式");
            }
        }
        else
        {
            fallbackReason = $"無法確定 repo（projectName='{projectName}', DefaultRepo='{_github.DefaultRepo}', Owner='{_github.Owner}'）";
            logger.LogWarning("Victoria 無法確定 repo（projectName={P}, DefaultRepo={D}），降級使用直接 LLM 模式",
                projectName, _github.DefaultRepo);
        }

        // ── 5. 組裝 GitHub 上下文（與 repo 路徑無關，先行取得） ──────────
        var githubContext = await BuildGitHubContextAsync(
            _github.Owner,
            string.IsNullOrWhiteSpace(projectName) ? _github.DefaultRepo : projectName,
            cancellationToken);

        // ── 6. 若有 repo 路徑，執行 Claude Code 模式；否則降級 ─────────
        CeoResponse? ceoResponse = null;

        if (repoPath is not null)
        {
            var claudeMd     = Path.Combine(repoPath, "CLAUDE.md");
            var claudeBak    = Path.Combine(repoPath, "CLAUDE.md.bak");
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Victoria.md");

            // 4a. Crash 自動修復：若 CLAUDE.md 開頭是 Victoria 標記代表上次未還原
            if (File.Exists(claudeMd))
            {
                try
                {
                    using var reader = new StreamReader(claudeMd);
                    var firstLine = await reader.ReadLineAsync(cancellationToken) ?? "";
                    if (firstLine.StartsWith("# Victoria", StringComparison.Ordinal))
                    {
                        logger.LogWarning("偵測到 Victoria CLAUDE.md 未還原（上次 crash？），嘗試從 .bak 還原");
                        if (File.Exists(claudeBak))
                            File.Copy(claudeBak, claudeMd, overwrite: true);
                        else
                            File.Delete(claudeMd);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "CLAUDE.md crash 修復失敗，繼續執行");
                }
            }

            var prompt = BuildVictoriaPrompt(
                userInput, projectName, agentList, rules, historyTurns, memories,
                availableProjects, githubContext, images);

            var apiKey = configuration["AITEAM_ANTHROPIC_KEY"]
                      ?? configuration["Anthropic:ApiKey"]
                      ?? "";
            var model  = configuration["Agents:CEO:Model"] ?? "claude-sonnet-4-6";

            ClaudeCodeResult? claudeResult = null;
            string? originalMd = null;

            await VictoriaLock.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(claudeMd))
                    originalMd = await File.ReadAllTextAsync(claudeMd, cancellationToken);

                // 備份（防 process kill 導致 CLAUDE.md 未還原）
                if (originalMd is not null)
                    await File.WriteAllTextAsync(claudeBak, originalMd, cancellationToken);

                // 寫入 Victoria 模板
                if (File.Exists(templatePath))
                    await File.WriteAllTextAsync(claudeMd,
                        await File.ReadAllTextAsync(templatePath, cancellationToken),
                        cancellationToken);

                claudeResult = await claudeCodeService.RunVictoriaAsync(
                    repoPath, prompt, model, apiKey, cancellationToken);
            }
            finally
            {
                try
                {
                    if (originalMd is not null)
                        await File.WriteAllTextAsync(claudeMd, originalMd, CancellationToken.None);
                    else if (File.Exists(claudeMd))
                        File.Delete(claudeMd);

                    if (File.Exists(claudeBak))
                        File.Delete(claudeBak);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "CLAUDE.md 還原失敗");
                }
                VictoriaLock.Release();
            }

            if (claudeResult is not null)
            {
                ceoResponse = TryParseActionBlock(claudeResult.Output)
                           ?? TryParseResponse(claudeResult.Output);
            }

            if (ceoResponse is null)
            {
                fallbackReason = $"Claude Code 回應解析失敗（Success={claudeResult?.Success}）";
                logger.LogWarning(
                    "Claude Code 回應解析失敗（Success={S}），降級使用直接 LLM",
                    claudeResult?.Success);
            }
        }

        // ── 7. LLM 降級（repoPath 為 null 或 Claude Code 回應解析失敗） ─
        if (ceoResponse is null)
        {
            var llmResponse = await ProcessAsync(
                userInput, projectName, agentList, rules,
                cancellationToken, images, null, availableProjects);

            // 附加診斷訊息，讓 Discord 可見降級原因（無需翻 Docker log）
            if (fallbackReason is not null)
                llmResponse.Reply = $"⚠️ *（LLM 降級模式：{fallbackReason}）*\n\n{llmResponse.Reply}";

            ceoResponse = llmResponse;
        }

        // ── 8. 持久化對話 turn ────────────────────────────────────────────
        await conversationRepository.AddTurnAsync(
            sessionId, userId, "user", userInput, cancellationToken);
        await conversationRepository.AddTurnAsync(
            sessionId, userId, "assistant", ceoResponse.Reply, cancellationToken);

        // ── 9. 持久化長期記憶 ─────────────────────────────────────────────
        if (ceoResponse.MemoriesToSave is { Count: > 0 })
        {
            var toSave = ceoResponse.MemoriesToSave
                .Select(m => new AiTeam.Data.Repositories.MemoryToSave(m.Content, m.Category))
                .ToList();
            await memoryRepository.SaveMemoriesAsync(userId, toSave, cancellationToken);
            logger.LogInformation("Victoria 儲存了 {Count} 筆長期記憶", toSave.Count);
        }

        return ceoResponse;
    }

    /// <summary>組裝 Victoria Claude Code 模式的完整 Prompt。</summary>
    private string BuildVictoriaPrompt(
        string userInput,
        string projectName,
        IReadOnlyList<AgentDescriptor> agentList,
        IReadOnlyList<string> rules,
        IReadOnlyList<CeoConversation> historyTurns,
        IReadOnlyList<AiTeam.Data.CeoMemory> memories,
        IReadOnlyList<string>? availableProjects,
        string githubContext,
        IReadOnlyList<ImageAttachment>? images)
    {
        var agents = string.Join("\n", agentList.Select(a =>
            string.IsNullOrWhiteSpace(a.Description) ? $"- {a.Name}" : $"- {a.Name}：{a.Description}"));

        var ruleList = rules.Count > 0
            ? string.Join("\n", rules.Select(r => $"- {r}"))
            : "（尚無規則）";

        var projectListBlock = availableProjects is { Count: > 1 }
            ? $"\n可用專案清單：{string.Join("、", availableProjects)}"
            : "";

        // 長期記憶（升冪排列供 Prompt 閱讀）
        var memoryBlock = memories.Count > 0
            ? string.Join("\n", memories
                .OrderBy(m => m.CreatedAt)
                .Select(m => $"[{m.Category}] {m.Content}"))
            : "（無長期記憶）";

        // Session 對話歷史
        var historyBlock = historyTurns.Count > 0
            ? string.Join("\n", historyTurns.Select(t =>
                t.Role == "user" ? $"[user] {t.Content}" : $"[assistant] {t.Content}"))
            : "（新 Session，無歷史）";

        // 圖片說明（若有）
        var imageNote = images is { Count: > 0 }
            ? $"\n\n（老闆附上了 {images.Count} 張圖片，請在分析時考慮圖片內容。）"
            : "";

        return $"""
            ## 可用 Agent
            {agents}

            ## 規則清單
            {ruleList}

            ## 當前專案
            {(string.IsNullOrWhiteSpace(projectName) ? "（未指定）" : projectName)}{projectListBlock}

            ## 近期 GitHub 上下文
            {githubContext}

            ## 你的長期記憶
            {memoryBlock}

            ## 本 Session 對話歷史
            {historyBlock}

            ## 老闆指令
            {userInput}{imageNote}
            """;
    }

    /// <summary>從 Claude Code 輸出中提取 &lt;ACTION&gt;...&lt;/ACTION&gt; XML 區塊並解析為 CeoResponse。</summary>
    private static CeoResponse? TryParseActionBlock(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return null;
        try
        {
            var match = Regex.Match(rawOutput, @"<ACTION>\s*(\{[\s\S]*?\})\s*</ACTION>");
            if (!match.Success) return null;
            return JsonSerializer.Deserialize<CeoResponse>(match.Groups[1].Value, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildSystemPrompt(IReadOnlyList<AgentDescriptor> agentList, IReadOnlyList<string> rules)
    {
        var agents = string.Join("\n", agentList.Select(a =>
            string.IsNullOrWhiteSpace(a.Description) ? $"- {a.Name}" : $"- {a.Name}：{a.Description}"));
        var ruleList = rules.Count > 0
            ? string.Join("\n", rules.Select(r => $"- {r}"))
            : "（尚無規則）";

        return $$"""
            你是 AI 團隊的 CEO Victoria，負責接收老闆指令、分析任務、分派給對應的 Agent。
            老闆會用自然語言直接對你說話，不一定使用固定格式。

            ## 可用 Agent
            {{agents}}

            ## 規則清單
            {{ruleList}}

            ## 第一步：智慧分類（每次回應前必須執行）
            在回應之前，先根據老闆的輸入與提供的系統上下文（GitHub PR/Issue 數量、近期任務記錄）進行分類：

            | 分類 | 判斷標準 | 行動 |
            |------|---------|------|
            | 新功能 | 目前沒有相關實作，是全新需求 | action = "propose"（先提案，不直接派工）|
            | Bug | 行為不符合預期，系統有異常 | action = "delegate"，target_agent = "Dev"，workflow_type = "bug_fix" |
            | 技術改善 | 重構、效能優化、技術債清理、程式碼整理，不新增功能也不是 Bug | action = "delegate"，target_agent = "Dev"，workflow_type = "tech_improvement" |
            | 操作指派 | 發版/建立 Release → target_agent = "Release"；部署/重啟/Rollback/維運 → target_agent = "Ops"；更新文件/寫說明/補 README → target_agent = "Doc" | action = "delegate"，workflow_type = null |
            | 取消任務 | 老闆要停止、取消、中斷目前正在執行的任務 | action = "cancel" |
            | 正常行為 | 行為符合設計，老闆不了解系統運作 | action = "reply"，解釋清楚 |
            | 疑問 | 老闆在問問題、請求解釋 | action = "reply"，直接回答 |

            在 reply 欄位的開頭**說明分類結果與理由**（一句話），例如：
            「Christ，這是一個新功能需求，因為目前系統中尚未有相關實作。」
            「Christ，這應該是個 Bug，登入流程不應該發生這個錯誤。」
            「Christ，這是技術改善需求，屬於重構而非新功能。」
            「Christ，這是正常行為，Vera 找不到 PR 是因為此 Project 目前沒有任何 open PR。」

            ## propose 模式（新功能專用）
            當判斷為新功能時，使用 action = "propose"：
            - 若資訊充足，直接進入提案
            - 若資訊不足，先用 action = "reply" 問一個關鍵問題再繼續
            - 若老闆是在回答你上一輪的反問（例如「C兩者都支援」、「用選項A」等簡短回覆），
              請根據對話歷史重新判斷原始需求的分類，不要因為這句話很短就改為 delegate；
              若原始需求是新功能，本輪依然應回傳 action = "propose"

            ## action 欄位規則（非常重要）
            - 老闆問問題、閒聊、或只需要你說明 → action = "reply"，target_agent = null
            - 老闆要求執行 Bug 修復 → action = "delegate"，target_agent = "Dev"，workflow_type = "bug_fix"
            - 老闆要求重構、效能優化、技術債清理 → action = "delegate"，target_agent = "Dev"，workflow_type = "tech_improvement"
            - 老闆要求發版/部署/更新文件 → action = "delegate"，target_agent = "Release"/"Ops"/"Doc"，workflow_type = null
            - 老闆提出新功能需求 → action = "propose"，target_agent = null，workflow_type = null
            - 老闆要停止/取消進行中任務 → action = "cancel"，target_agent = null，task = null，workflow_type = null
            - 只要你打算派任務給任何 Agent，action 就必須是 "delegate"，不得使用 "reply"
            - 禁止在 reply 欄位描述「已分派給 X 處理」卻把 action 設為 "reply"

            ## 反問機制（非常重要）
            - 當老闆提供的資訊不足以確定要做什麼或針對哪個專案時，使用 action = "reply" 反問
            - 每次只問一個最關鍵的問題，不可以一次問多個問題
            - 提供目前可用的選項供老闆快速回答（例如列出現有專案名稱）
            - 禁止猜測老闆的意圖，寧可反問也不要猜錯
            - **專案確認規則**：若「當前專案」為未指定，且「可用專案清單」有兩個以上，
              必須先用 action = "reply" 詢問老闆這個需求屬於哪個專案，列出清單供選擇，
              確認專案後才繼續分類與處理

            ## 回應格式
            你必須只回傳以下 JSON 格式，不得包含任何其他文字：
            {
              "reply": "給老闆看的回應訊息（繁體中文，開頭說明分類與理由）",
              "action": "reply | delegate | propose | cancel",
              "target_agent": "Dev | Ops | QA | Doc | Requirements | Reviewer | Release | Designer | null",
              "workflow_type": "bug_fix | tech_improvement | null",
              "task": {
                "title": "任務標題",
                "project": "專案名稱",
                "description": "詳細描述",
                "priority": "low | normal | high | critical"
              },
              "require_confirmation": true
            }
            """;
    }

    private async Task<string> BuildUserMessageAsync(
        string userInput,
        string projectName,
        IReadOnlyList<ConversationTurn>? history,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? availableProjects = null)
    {
        var recentTasks = await taskRepository.GetRecentByProjectAsync(projectName, limit: 5, cancellationToken);
        var taskHistory = recentTasks.Count > 0
            ? string.Join("\n", recentTasks.Select(t => $"- [{t.Status}] {t.Title}（{t.AssignedAgent}）"))
            : "（無近期任務紀錄）";

        // 查詢 GitHub PR / Issue 上下文（供 CEO 分類判斷用）
        var repo = string.IsNullOrWhiteSpace(projectName) ? _github.DefaultRepo : projectName;
        var githubContext = await BuildGitHubContextAsync(_github.Owner, repo, cancellationToken);

        // 若有對話歷史，插入在指令前面讓 CEO 知道上下文
        var historyBlock = "";
        if (history is { Count: > 0 })
        {
            var turns = string.Join("\n", history.Select(t =>
                t.Role == "user" ? $"老闆：{t.Content}" : $"CEO：{t.Content}"));
            historyBlock = $"""

                ## 對話歷史（最近幾輪）
                {turns}

                """;
        }

        var projectListBlock = availableProjects is { Count: > 1 }
            ? $"可用專案清單：{string.Join("、", availableProjects)}"
            : "";

        return $"""
            ## 當前專案
            {(string.IsNullOrWhiteSpace(projectName) ? "（未指定）" : projectName)}
            {projectListBlock}

            ## 近期相關任務紀錄
            {taskHistory}

            ## GitHub 系統上下文（供分類判斷使用）
            {githubContext}
            {historyBlock}
            ## 老闆指令
            {userInput}
            """;
    }

    private async Task<string> BuildGitHubContextAsync(
        string owner, string repo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            return "（GitHub 設定未完整，無法取得 PR/Issue 資訊）";

        try
        {
            var prsTask    = gitHubService.ListOpenPullRequestsAsync(owner, repo);
            var issuesTask = gitHubService.ListOpenIssuesAsync(owner, repo);
            await Task.WhenAll(prsTask, issuesTask);

            var prs    = prsTask.Result;
            var issues = issuesTask.Result;

            var prList = prs.Count > 0
                ? string.Join("\n", prs.Take(10).Select(p => $"  - PR #{p.Number}：{p.Title}"))
                : "  （無 open PR）";
            var issueList = issues.Count > 0
                ? string.Join("\n", issues.Take(10).Select(i => $"  - Issue #{i.Number}：{i.Title}"))
                : "  （無 open Issue）";

            return $"""
                Repo：{owner}/{repo}
                Open PR（共 {prs.Count} 筆）：
                {prList}
                Open Issue（共 {issues.Count} 筆）：
                {issueList}
                """;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "取得 GitHub 上下文失敗");
            return "（GitHub 上下文取得失敗）";
        }
    }

    private static CeoResponse? TryParseResponse(string content)
    {
        try
        {
            // Stage 13：優先從 code fence 提取，fallback 至 IndexOf('{')
            var fenceMatch = Regex.Match(content, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
            string json;
            if (fenceMatch.Success)
            {
                json = fenceMatch.Groups[1].Value;
            }
            else
            {
                var start = content.IndexOf('{');
                var end   = content.LastIndexOf('}');
                if (start < 0 || end < 0) return null;
                json = content[start..(end + 1)];
            }
            return JsonSerializer.Deserialize<CeoResponse>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
