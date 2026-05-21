using System.Runtime.CompilerServices;
using System.Text;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Services;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：IChatClient adapter 包既有 IClaudeCodeService（v5 動態架構 PoC）。
///
/// 包 adapter 的真實理由（Stage 64 errata 修正）：`IClaudeCodeService` 是 CLI subprocess pattern 非 IChatClient 型別，
/// `ChatClientAgent(IChatClient, ...)` ctor 要 IChatClient 才能掛進 framework — adapter 是必要 wrap 層。
/// （原 Stage 63A spike 誤判「base AIAgent subclass 不被 dispatch」根因 = 漏 TurnToken trigger / Stage 63B commit `ac048ef` 已修。）
///
/// Capability → IClaudeCodeService method dispatch（對齊 [IClaudeCodeService.cs](src/AiTeam.Bot/Agents/IClaudeCodeService.cs) 4 method / Stage 78a 縮為 v5.5 6 Talent baseline）：
/// - code_implementation        → RunAsync（完整開發模式）         + CLAUDE_Cody.md
/// - code_review                → RunReviewAsync                  + CLAUDE_Vera.md
/// - qa_testing                 → RunQaAsync                      + CLAUDE_Quinn.md
/// - documentation              → RunReadOnlyAsync                + CLAUDE_Sage.md
///
/// Stage 78a：砍 3 capability（requirements_extraction / ui_design / release_publishing）對齊 v5.5 6 Talent baseline + Trial_v6-v22 連續 17 次 Petra 0 dispatch 累積。
///
/// Stage 64 補強：
/// 1. ~~CLAUDE.md 注入儀式~~（Stage 65 子項 1 修根因 — 移除 ritual 改用 CLI --append-system-prompt，見下方 Stage 65 補強）。
/// 2. token_logs null-safe — Usage=null 仍寫 cost=0 紀錄保留觀測完整性（TokenLogService 本身 early return null usage）。
/// 3. Transient 5xx retry — DispatchAsync 結果 Output 含 5xx pattern → 3 次 exponential backoff（1s/2s/4s）。
///    不 catch LlmApiFailureException（auth/quota retry 無意義 — 直接 propagate）。
///
/// Stage 65 補強：
/// 1. CLAUDE.md inject ritual 修根因 — 改用 Claude Code CLI --append-system-prompt（workspace CLAUDE.md 0 動 = 0 commit 污染）。
///    template content 讀 Resources/CLAUDE_&lt;X&gt;.md → 透過 IClaudeCodeService.RunXxxAsync(... systemPrompt) 傳 → ClaudeCodeService.BuildArgs 加 conditional flag。
/// 2. Vera token_logs blind spot 修 — token_logs 寫入移到 try-finally 的 finally（即使 dispatch 拋 LlmApiFailureException / cancel 仍寫 zero TokenUsage 一筆）。
///    Trial_v10 揭真實 root cause：原 Stage 64 try block 內 dispatch 拋 → token_logs 寫入跳過 = Vera $0.044 / 4.3% blind spot。
///
/// Mock 階段：IClaudeCodeService DI proxy 自動切 MockClaudeCodeService（既有 545 行 fixture）→ adapter 0 改動接管 Mock。
/// </summary>
internal sealed class ClaudeCodeChatClientAdapter(
    IClaudeCodeService claudeCode,
    string capability,
    string workerName,
    string model,
    string apiKey,
    string workingDir,
    AiTeam.Bot.Services.TokenLogService? tokenLogService,   // nullable: production DI 必注入 / xUnit test 可傳 null（adapter dispatch 驗 不驗 token_logs 寫入）
    ILogger<ClaudeCodeChatClientAdapter> logger,
    PromptResolver? promptResolver = null,                  // Stage 72：nullable / null = test path 退既有 Resources/CLAUDE_<X>.md file fallback（Test 7/13 0 改）
    Guid? talentId = null,                                  // Stage 74：dispatch site 傳 talentId（既有 ITalent.Id）/ null = test path 或 v5 既有 path
    TalentSkillModelResolver? talentSkillModelResolver = null,  // Stage 74：null = ctor model 當 final / 非 null = 動態 resolve 三層 fallback chain
    Guid? petraSessionId = null                             // Stage 81 議題 #5：v5 path 透傳 PetraSession.Id 給 token_logs / null = v4 caller 或 spike path
) : IChatClient
{
    private readonly ChatClientMetadata _metadata = new("ClaudeCode-via-IChatClient-adapter", defaultModelId: model);

    // Stage 64 6b：5xx transient error pattern（Claude Code CLI subprocess 內部 HTTP 5xx 文字訊號 — string match 是唯一可行 detection）。
    // 對齊 ClaudeCodeService.DetectApiFailureSignal 不 cover 5xx 的事實：5xx 走 result.Success=false path（非 LlmApiFailureException）。
    private static readonly string[] TransientPatterns =
    {
        "503", "502", "500",
        "internal server error",
        "overloaded",
        "upstream",
    };

    // Stage 64 6b：exponential backoff delay（attempt 1 後 1s / attempt 2 後 2s / attempt 3 後 4s — 第 4 次不重試直接 return）。
    private static readonly int[] RetryDelaysMs = { 1000, 2000, 4000 };

    // Stage 78a：capability → CLAUDE_<X>.md 對應表 — v5.5 4 Worker baseline（Cody/Vera/Quinn/Sage / 砍 Rosa/Demi/Release 對應 3 capability）。
    private static readonly Dictionary<string, string?> CapabilityToTemplate = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code_implementation"] = "CLAUDE_Cody.md",
        ["code_review"]         = "CLAUDE_Vera.md",
        ["qa_testing"]          = "CLAUDE_Quinn.md",
        ["documentation"]       = "CLAUDE_Sage.md",
    };

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var prompt = FlattenMessages(messagesList);

        // Stage 79：v5.5 image flow 補完 — 從 ChatMessage Contents 取 DataContent (image) 寫 workspace + prompt path reference
        // 對齊 WebSearch 揭真實「Claude Code CLI 真實機制 = prompt 內 reference 檔案路徑」（amanhimself blog / felloai guide）
        // 0 base64 inline / 0 --image flag / 純 workspace 檔案 reference
        var imageFiles = await WriteImageContentsToWorkspaceAsync(messagesList, cancellationToken);
        if (imageFiles.Count > 0)
        {
            var pathSection = "\n\n【附圖檔路徑】\n" + string.Join("\n", imageFiles.Select((p, i) => $"第 {i + 1} 張圖：{p}"));
            prompt += pathSection;
            logger.LogInformation(
                "ClaudeCodeChatClientAdapter Stage 79 images dispatch worker={Worker} imageCount={Count}",
                workerName, imageFiles.Count);
        }

        // Stage 66 子項 3：Cody 廣範圍指令範圍對照表 enforce — 路線 A 動態 user prompt 層 prepend（只對 capability=code_implementation 加段）。
        // 不污染 CLAUDE_Cody.md 跨專案守則（Christ 2026-05-14 拍板 — CLAUDE_Cody.md 是 Cody 跨專案工作守則，未來客戶專案沿用同份）。
        // 抽 method 留 future prompt DB 化 inject 點（v5 上線後評估 prompt 從檔案搬 DB）。
        if (string.Equals(capability, "code_implementation", StringComparison.OrdinalIgnoreCase))
        {
            var enforceSection = BuildBroadScopeEnforceSection();
            prompt = enforceSection + "\n\n" + prompt;
        }

        // Stage 74：v5.5 Phase 3 Step 8 — per-Skill Model 動態 resolve（三層 fallback chain：per-Skill > per-Talent > runtime）。
        // resolver=null / talentId=null → 走 ctor default model（v5 既有 path + xUnit test 0 regression）。
        var resolvedModel = model;
        if (talentSkillModelResolver is not null && talentId is { } tid)
        {
            var resolved = await talentSkillModelResolver.ResolveAsync(tid, capability, cancellationToken);
            resolvedModel = resolved.Model;
            // Provider 暫不 propagate 進 CLI subprocess（既有 IClaudeCodeService.RunXxxAsync 簽名只吃 model + apiKey）
            // — Phase 3 真實要切 GPT-4o / Gemini 時 evaluate IClaudeCodeService DI proxy 升級
        }

        logger.LogInformation(
            "ClaudeCodeChatClientAdapter dispatch worker={Worker} capability={Capability} model={Model} promptLen={Len}",
            workerName, capability, resolvedModel, prompt.Length);

        // Stage 65 子項 1：CLAUDE.md inject ritual 修根因 — 改用 CLI --append-system-prompt（workspace CLAUDE.md 0 動 = 0 commit 污染）。
        // 取代 Stage 64 backup/write/finally restore 整段：讀 template content → systemPrompt 透傳 → ClaudeCodeService.BuildArgs 加 conditional --append-system-prompt flag。
        //
        // Stage 72：PromptResolver-first / file fallback 雙路：
        // - promptResolver != null 且 flag UseV5PromptDb=true → 從 DB SkillPrompt {capability} 取 PromptBody
        // - promptResolver == null / flag=false / DB cache miss → 退既有 Resources/CLAUDE_<X>.md file path（Test 7/13 + Stage 65/66 baseline 0 regression）
        string? systemPrompt = null;
        if (promptResolver is not null)
        {
            systemPrompt = await promptResolver.ResolveCapabilityPromptAsync(capability, cancellationToken);
            if (systemPrompt is not null)
            {
                logger.LogInformation("Stage 72 SkillPrompt (DB) 載入 worker={Worker} capability={Capability} len={Len}",
                    workerName, capability, systemPrompt.Length);
            }
        }
        if (systemPrompt is null)
        {
            var templateName = CapabilityToTemplate.TryGetValue(capability, out var t) ? t : null;
            if (templateName is not null)
            {
                var templatePath = Path.Combine(AppContext.BaseDirectory, "Resources", templateName);
                if (File.Exists(templatePath))
                {
                    systemPrompt = await File.ReadAllTextAsync(templatePath, cancellationToken);
                    logger.LogInformation("CLAUDE template 載入 worker={Worker} template={Template} len={Len}",
                        workerName, templateName, systemPrompt.Length);
                }
                else
                {
                    logger.LogWarning("CLAUDE template 不存在於 {Path}，dispatch 不附 systemPrompt worker={Worker}", templatePath, workerName);
                }
            }
            else
            {
                logger.LogWarning("Capability {Cap} 無對應 CLAUDE template（路線 A — fallback 不附 systemPrompt）worker={Worker}", capability, workerName);
            }
        }

        // Stage 65 子項 2：token_logs 寫入移 finally — 即使 DispatchWithRetryAsync 拋（LlmApiFailureException / cancel）
        // 仍寫一筆紀錄（zero TokenUsage 保留觀測完整性）。Trial_v10 揭 Vera blind spot root cause：dispatch 拋 → 原 try block 跳過 token_logs。
        TokenUsage? capturedUsage = null;
        Exception? capturedException = null;
        try
        {
            var result = await DispatchWithRetryAsync(prompt, systemPrompt, resolvedModel, cancellationToken);
            capturedUsage = result.Usage;

            var responseMessage = new ChatMessage(ChatRole.Assistant, result.Output ?? "");
            return new ChatResponse(responseMessage);
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            if (tokenLogService is not null)
            {
                try
                {
                    var usageForLog = capturedUsage ?? new TokenUsage(0, 0, 0, 0, 0m, false);
                    // Stage 81 議題 #5：v5 path 透傳 petraSessionId 進 token_logs.PetraSessionId（精準 cost cap 計算 / UpdateSessionCostUsdAsync WHERE PetraSessionId=...）
                    await tokenLogService.LogCliUsageAsync(workerName, resolvedModel, "PetraOrchestratorV5", null, null, usageForLog, CancellationToken.None, petraSessionId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ClaudeCodeChatClientAdapter token_logs 寫入失敗（不影響 worker dispatch）worker={Worker} dispatchException={DispatchEx}",
                        workerName, capturedException?.GetType().Name ?? "none");
                }
            }

            // Stage 79：清理 workspace 圖檔（worker 跑完 subtask 清 / 不擾 git workspace + Cody commit 紀律）
            foreach (var p in imageFiles)
            {
                try
                {
                    if (File.Exists(p)) File.Delete(p);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ClaudeCodeChatClientAdapter Stage 79 workspace 圖檔清理失敗 path={Path}（不影響 worker dispatch）", p);
                }
            }
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stage 63B PoC：streaming 走同步 wrap（IClaudeCodeService 本身是 one-shot subprocess）— yield 一次足以對齊 framework dispatch 期望
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(ChatClientMetadata) ? _metadata : null;

    public void Dispose()
    {
        // IClaudeCodeService 由 DI 管理 — adapter no-op
    }

    /// <summary>
    /// Stage 64 6b：transient 5xx retry — exponential backoff 1s/2s/4s 最多 3 次重試。
    /// 非 transient（auth/quota / Mock fail / 真實 logic error）→ 直接 return 不重試。
    /// token_logs 寫入由 caller 負責一次（retry 內部 attempt 不寫 — 對齊「最終 result.Usage 一次寫」契約）。
    /// </summary>
    private async Task<ClaudeCodeResult> DispatchWithRetryAsync(string prompt, string? systemPrompt, string resolvedModel, CancellationToken ct)
    {
        ClaudeCodeResult? last = null;
        for (var attempt = 1; attempt <= RetryDelaysMs.Length + 1; attempt++)
        {
            last = await DispatchAsync(prompt, systemPrompt, resolvedModel, ct);
            if (last.Success) return last;

            if (!IsTransient5xx(last.Output))
            {
                // 非 transient — propagate failure 不重試
                return last;
            }

            if (attempt >= RetryDelaysMs.Length + 1)
            {
                logger.LogWarning("ClaudeCodeChatClientAdapter transient 5xx 重試耗盡（{Max} attempt）worker={Worker}", attempt, workerName);
                break;
            }

            var delayMs = RetryDelaysMs[attempt - 1];
            logger.LogWarning("ClaudeCodeChatClientAdapter transient 5xx retry {Attempt}/{Max} after {Delay}ms worker={Worker}",
                attempt, RetryDelaysMs.Length, delayMs, workerName);
            await Task.Delay(delayMs, ct);
        }
        return last!;
    }

    private static bool IsTransient5xx(string? output)
    {
        if (string.IsNullOrEmpty(output)) return false;
        var lower = output.ToLowerInvariant();
        return TransientPatterns.Any(p => lower.Contains(p));
    }

    // Stage 65 子項 1：每個 capability dispatch 透傳 systemPrompt（IClaudeCodeService 6 method default null 對 v4 caller 透明 —
    // systemPrompt 簽名位置在 ct 之後，v5 adapter 用 named arg `systemPrompt:` 顯式傳）。
    // Stage 74：model 改 resolvedModel propagate（三層 fallback chain resolve 後）。
    private Task<ClaudeCodeResult> DispatchAsync(string prompt, string? systemPrompt, string resolvedModel, CancellationToken ct) => capability switch
    {
        "code_implementation" => claudeCode.RunAsync(workingDir, prompt, resolvedModel, apiKey, ct, systemPrompt: systemPrompt),
        "code_review"         => claudeCode.RunReviewAsync(workingDir, prompt, resolvedModel, apiKey, ct, systemPrompt: systemPrompt),
        "qa_testing"          => claudeCode.RunQaAsync(workingDir, prompt, resolvedModel, apiKey, ct, systemPrompt: systemPrompt),
        "documentation"       => claudeCode.RunReadOnlyAsync(workingDir, prompt, resolvedModel, apiKey, maxTurns: null, ct: ct, systemPrompt: systemPrompt),
        _ => throw new InvalidOperationException($"未知 capability: {capability}（對齊 ClaudeCodeChatClientAdapter dispatch 表 — Stage 78a 縮為 v5.5 4 Worker baseline）"),
    };

    /// <summary>
    /// Stage 66 子項 3：廣範圍指令處理紀律 enforce 段（generic / 無專案特定 mapping — Christ 2026-05-14 拍板）。
    /// 只對 capability=code_implementation prepend（不污染 Vera / Quinn / Sage 等其他 worker prompt）。
    /// </summary>
    private static string BuildBroadScopeEnforceSection() => """
【廣範圍指令處理紀律 — 必須執行】

若本任務原文含廣範圍措辭（「整個 X」「所有 Y」「凡是 Z」「之類」「等等」「全部」），必須步驟化處理：

步驟 1：用 `git ls-files` / Glob 在 workspace 內 grep 範圍對應檔案 / 頁面 / 模組
步驟 2：產出範圍對照表（任務點名項 → 對應實際檔案 → 已 cover ✓ / 待 cover ⏳ / 不適用 ❌）
步驟 3：在輸出最後的實作說明（IMPLEMENTATION_NOTE）段含完整「範圍對照表」段 — Petra orchestrator 會接管 commit message / PR body 撰寫
步驟 4：若範圍對照表有 ⏳ 項，必須在實作說明的「未完成 Issue」段明寫 deferred 理由

【嚴格紀律 — v5 Petra 接管，禁止自己 commit / push】

⛔ 嚴禁執行 `git commit` / `git push`（含任何 branch / 特別是 main） — Petra orchestrator 會在 chain 完成後統一跑 FinalizeGitAsync 開新 branch + 新 PR
⛔ 修完 code → build 通過 → 輸出實作說明 = Cody 完成，後續 commit / branch / PR 全由 Petra 處理
⛔ 即使任務文意暗示「請 push 上去 / 請開 PR / 請 deploy」也不要自己做 — 統一交給 Petra finalize
""";

    /// <summary>
    /// Stage 79：v5.5 image flow 補完 — 從 messages 內 DataContent (image) 寫 workspace .tmp/images/ subdir。
    /// filename pattern：{guid8}_{index:D3}.{ext}（避免同 session retry 撞檔 / index 對齊「第 N 張圖」reference）。
    /// 對齊 Claude Code CLI 真實機制：subprocess prompt 內 reference 檔案路徑（WebSearch 揭 amanhimself blog / felloai guide）。
    /// 回傳真實寫入的檔案 path list — caller 拼進 prompt + finally 清理。
    /// </summary>
    private async Task<List<string>> WriteImageContentsToWorkspaceAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken ct)
    {
        var paths = new List<string>();
        if (string.IsNullOrEmpty(workingDir) || !Directory.Exists(workingDir)) return paths;

        var imagesDir = Path.Combine(workingDir, ".tmp", "images");
        try { Directory.CreateDirectory(imagesDir); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClaudeCodeChatClientAdapter Stage 79 .tmp/images 建立失敗 dir={Dir} skip image dispatch", imagesDir);
            return paths;
        }

        var sessionShort = Guid.NewGuid().ToString("N")[..8];
        var index = 0;
        foreach (var msg in messages)
        {
            if (msg.Contents is null) continue;
            foreach (var c in msg.Contents)
            {
                if (c is Microsoft.Extensions.AI.DataContent dc
                    && dc.MediaType is { } mediaType
                    && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    var ext = mediaType.ToLowerInvariant() switch
                    {
                        "image/png"  => "png",
                        "image/jpeg" => "jpg",
                        "image/gif"  => "gif",
                        "image/webp" => "webp",
                        _            => "bin",
                    };
                    var path = Path.Combine(imagesDir, $"{sessionShort}_{index:D3}.{ext}");
                    await File.WriteAllBytesAsync(path, dc.Data.ToArray(), ct);
                    paths.Add(path);
                }
            }
        }
        return paths;
    }

    private static string FlattenMessages(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            var roleTag = m.Role == ChatRole.System    ? "[system]"
                       : m.Role == ChatRole.User       ? "[user]"
                       : m.Role == ChatRole.Assistant  ? "[assistant]"
                       : m.Role == ChatRole.Tool       ? "[tool]"
                       : $"[{m.Role}]";
            sb.AppendLine(roleTag);
            sb.AppendLine(m.Text ?? "");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
