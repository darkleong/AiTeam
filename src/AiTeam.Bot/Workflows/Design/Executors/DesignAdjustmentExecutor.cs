using System.Text;
using System.Text.Json;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using AiTeam.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：needs_adjustment 子流程 B2 single-Executor wrapper（v4 漸進遷移第四步）。
///
/// 職責（議題 B2 拍板）：
///   - 接 DesignPetraVerdict（decision="needs_adjustment"）
///   - 內部跑 Rosa adjust（含 GitHub Issue 第二批建立）+ Demi adjust（含 DemiSessionId 動態建立邊界，對齊 legacy line 487-500）+ Petra eval 三 LLM call
///   - 兩出口（B2 拍板，三件套：兩 [SendsMessage] + partial class + 註解）：
///     * Approved 路徑（議題 7 必修）：Petra eval=approved → 內部判 evalDecision.DesignPlan 是否為空：
///         - 不為空 → 直接帶進 DesignAdjustmentApproved record
///         - 為空 → fallback 走 BuildDesignPetraPlanPrompt + Petra session resume 補產（對齊 legacy line 549-551）→ 帶進 record
///         - SendMessageAsync(DesignAdjustmentApproved with non-empty DesignPlan) → DesignPlanExecutor.HandleAdjustmentApprovedAsync 直接 wrap
///     * needs_meeting 路徑（議題 6 必修）：Petra eval=needs_meeting → Executor 內**先處理 escalate 邊界**（對齊 legacy line 290-298）：
///         - if state.Round >= state.MaxRounds → 送 DesignPetraVerdict { Decision="escalate", EscalateReason="多輪調整後仍需重開會議，已達設計會議上限" } → AddSwitch 路由 DesignEscalateExecutor
///         - else → 送 DesignPetraVerdict { Decision="needs_discussion", Round=state.Round } → AddSwitch case `needs_discussion < max` → DesignRoundStartExecutor loop back
///
/// 對齊 Stage 50 踩坑 #10 三件套紀律 + Stage 51 MidInterruptCheckExecutor 雙 [SendsMessage] partial class pattern。
/// </summary>
[SendsMessage(typeof(DesignAdjustmentApproved))]
[SendsMessage(typeof(DesignPetraVerdict))]
internal sealed partial class DesignAdjustmentExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignAdjustmentExecutor> _logger;

    public DesignAdjustmentExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignAdjustmentExecutor> logger)
        : base("Design-Adjustment")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleAsync(DesignPetraVerdict verdict, IWorkflowContext context)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        // Stage 37 搭車修：Petra JSON 回應可能漏填 AdjustmentTargets / AdjustmentInstructions
        // （record 欄位雖非 nullable，但 System.Text.Json 不強制 runtime non-null），用 defensive defaults
        var adjustmentTargets      = verdict.AdjustmentTargets      ?? [];
        var adjustmentInstructions = verdict.AdjustmentInstructions ?? new Dictionary<string, string>();

        if (verdict.AdjustmentTargets is null || verdict.AdjustmentInstructions is null)
            _logger.LogWarning(
                "[Stage52] Petra 回應 needs_adjustment 但缺 AdjustmentTargets/Instructions（GroupId={Id}），已退化為 no-op 調整輪",
                state.GroupId);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp        = scope.ServiceProvider;
        var commons   = sp.GetRequiredService<MeetingCommons>();
        var tokenLog  = sp.GetRequiredService<TokenLogService>();
        var config    = sp.GetRequiredService<IConfiguration>();
        var ghService = sp.GetRequiredService<GitHubService>();

        var apiKey   = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var modelPM  = config["Agents:PM:Model"]          ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";
        var modelReq = config["Agents:Requirements:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";
        var modelDes = config["Agents:Designer:Model"]     ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";

        var logSb = new StringBuilder(state.MeetingLog);
        logSb.AppendLine("## 調整紀錄").AppendLine();
        logSb.AppendLine("### Petra 修改指示");
        logSb.AppendLine(JsonSerializer.Serialize(adjustmentInstructions,
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        logSb.AppendLine();

        var updatedRosaOutput = "";
        var updatedDemiOutput = "";

        // ── Rosa 調整 ──
        if (adjustmentTargets.Any(t => t.Equals("rosa", StringComparison.OrdinalIgnoreCase)))
        {
            var instruction = adjustmentInstructions.GetValueOrDefault("rosa", "請根據會議討論修改 Issues");
            var prompt = $"Petra 的修改指示：\n\n{instruction}\n\n請調整你的 GitHub Issues，在回應最後以 JSON Array 格式輸出更新後的完整 Issues 清單（格式同前置作業）。";
            updatedRosaOutput = await commons.RunAgentTurnAsync(
                "Rosa", state.RosaSessionId, prompt, modelReq, apiKey,
                isFirstMessage: false,
                workingDir: state.WorkingDir,
                allowedTools: MeetingCommons.ReadOnlyTools,
                ct: default,
                meetingType: "Design",
                round: state.Round,
                tokenLogService: tokenLog);

            logSb.AppendLine("### Rosa 調整結果").AppendLine(updatedRosaOutput).AppendLine();

            var updatedParsed = DesignPrompts.TryParseDesignIssues(updatedRosaOutput);
            if (updatedParsed is { Count: > 0 })
            {
                state.IssuesJson = JsonSerializer.Serialize(updatedParsed);

                // Stage 54 B2 idempotency check：Crash Recovery 重跑時若 LastIssueCreatedRound == state.Round 表示本輪已創過 → 跳過
                var db = sp.GetRequiredService<AppDbContext>();
                var lastCreatedRound = await db.TaskGroups
                    .Where(g => g.Id == state.GroupId)
                    .Select(g => g.LastIssueCreatedRound)
                    .FirstOrDefaultAsync();

                if (lastCreatedRound == state.Round)
                {
                    _logger.LogInformation(
                        "[Stage54] Recovery 重跑偵測 LastIssueCreatedRound == Round={Round}，跳過 Adjust Rosa GitHub Issue 創建（GroupId={Id}）",
                        state.Round, state.GroupId);
                }
                else
                {
                    var urlList = new List<string>();
                    foreach (var issue in updatedParsed)
                    {
                        try
                        {
                            var url = await ghService.CreateIssueAsync(state.Owner, state.Repo, issue.Title, issue.Body, issue.Labels);
                            urlList.Add(url);
                        }
                        catch (Exception ex) { _logger.LogWarning(ex, "[Stage52] Adjust Rosa GitHub Issue 失敗：{Title}", issue.Title); }
                    }
                    if (urlList.Count > 0)
                    {
                        state.IssueUrls = JsonSerializer.Serialize(urlList);
                        // Stage 54 B2：set marker = state.Round（本輪 Adjustment 已創）
                        var thisRound = state.Round;
                        await db.TaskGroups.Where(g => g.Id == state.GroupId)
                            .ExecuteUpdateAsync(s => s.SetProperty(g => g.LastIssueCreatedRound, (int?)thisRound));
                    }
                }
            }
        }

        // ── Demi 調整 ──
        if (adjustmentTargets.Any(t => t.Equals("demi", StringComparison.OrdinalIgnoreCase)))
        {
            var instruction = adjustmentInstructions.GetValueOrDefault("demi", "請根據會議討論修改 UI 規格");

            if (state.DemiSessionId is null)
            {
                // 邊界案例：初始 needsDemi=false 但會議揭露需要 UI 規格 → 動態建立 DemiSessionId（對齊 legacy line 487-500）
                state.DemiSessionId = Guid.NewGuid().ToString();
                var createPrompt =
                    $"你是 Demi，負責 UI/UX 設計。設計會議發現此任務需要 UI 規格。\n\n" +
                    $"## 任務計劃書\n{state.TaskPlan}\n\n" +
                    $"## Petra 的指示\n{instruction}\n\n" +
                    $"請探索相關 codebase 後，產出完整的 UI/UX 規格（Markdown 格式）。";
                updatedDemiOutput = await commons.RunAgentTurnAsync(
                    "Demi", state.DemiSessionId, createPrompt, modelDes, apiKey,
                    isFirstMessage: true,
                    workingDir: state.WorkingDir,
                    allowedTools: MeetingCommons.ReadOnlyTools,
                    ct: default,
                    maxTurns: 25,
                    meetingType: "Design",
                    round: state.Round,
                    tokenLogService: tokenLog);
            }
            else
            {
                var adjustPrompt = $"Petra 的修改指示：\n\n{instruction}\n\n請調整你的 UI 規格，回應更新後的完整 UI/UX 規格（Markdown 格式）。";
                updatedDemiOutput = await commons.RunAgentTurnAsync(
                    "Demi", state.DemiSessionId, adjustPrompt, modelDes, apiKey,
                    isFirstMessage: false,
                    workingDir: state.WorkingDir,
                    allowedTools: MeetingCommons.ReadOnlyTools,
                    ct: default,
                    meetingType: "Design",
                    round: state.Round,
                    tokenLogService: tokenLog);
            }

            state.UiSpecContent = updatedDemiOutput;
            logSb.AppendLine("### Demi 調整結果").AppendLine(updatedDemiOutput).AppendLine();
        }

        // ── Petra 評估修改幅度 ──
        var evalSb = new StringBuilder();
        evalSb.AppendLine("Rosa 和 Demi 已完成修改。以下是調整後的內容：").AppendLine();
        if (!string.IsNullOrEmpty(updatedRosaOutput))
        {
            evalSb.AppendLine("### Rosa 調整後的 Issues").AppendLine(updatedRosaOutput).AppendLine();
        }
        if (!string.IsNullOrEmpty(updatedDemiOutput))
        {
            evalSb.AppendLine("### Demi 調整後的 UI 規格").AppendLine(updatedDemiOutput).AppendLine();
        }
        evalSb.AppendLine("請評估修改幅度，在回應最後輸出以下 JSON（單獨一行，不加 code block）：");
        evalSb.AppendLine("{\"evaluation\":\"approved|needs_meeting\",\"design_plan\":\"（approved 時輸出完整設計規劃書，needs_meeting 時留空）\",\"reason\":\"reason\"}");
        evalSb.AppendLine("- approved：修改幅度小，Petra 自審通過 → 輸出完整設計規劃書");
        evalSb.AppendLine("- needs_meeting：修改幅度大，需重開設計會議");

        var petraEvalOutput = await commons.RunAgentTurnAsync(
            "Petra", state.PetraSessionId, evalSb.ToString(), modelPM, apiKey,
            isFirstMessage: false,
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: default,
            meetingType: "Design",
            round: state.Round,
            tokenLogService: tokenLog);

        logSb.AppendLine("### Petra 評估").AppendLine(petraEvalOutput).AppendLine();

        var evalDecision = DesignPrompts.TryParseDesignAdjustmentEvaluation(petraEvalOutput);
        var isApproved   = evalDecision is null || evalDecision.Evaluation == "approved";

        // ============================================================
        // Approved 路徑（議題 7 必修）
        // ============================================================
        if (isApproved)
        {
            var designPlan = evalDecision?.DesignPlan ?? "";
            if (string.IsNullOrWhiteSpace(designPlan))
            {
                // fallback：Petra eval 沒帶 DesignPlan → 走 BuildDesignPetraPlanPrompt 補產（對齊 legacy line 549-551）
                designPlan = await commons.RunAgentTurnAsync(
                    "Petra", state.PetraSessionId,
                    DesignPrompts.BuildDesignPetraPlanPrompt(state.TaskPlan, state.IssuesJson, state.UiSpecContent),
                    modelPM, apiKey,
                    isFirstMessage: false,
                    workingDir: state.WorkingDir,
                    allowedTools: MeetingCommons.ReadOnlyTools,
                    ct: default,
                    meetingType: "Design",
                    round: state.Round,
                    tokenLogService: tokenLog);
            }

            // 寫進 state（DesignPlanExecutor 不改 state，只 wrap result）
            state.DesignPlan    = designPlan;
            state.MeetingLog    = logSb.ToString();
            state.TotalRounds   = state.Round;
            await DesignStateHelpers.SaveAsync(context, state);

            _logger.LogInformation(
                "[Stage52] Adjustment approved（GroupId={Id}，round={Round}）",
                state.GroupId, state.Round);

            await context.SendMessageAsync(new DesignAdjustmentApproved(
                Round:           state.Round,
                MaxRounds:       state.MaxRounds,
                DesignPlan:      designPlan,
                PetraEvalOutput: petraEvalOutput,
                MeetingLog:      state.MeetingLog));
            return;
        }

        // ============================================================
        // needs_meeting 路徑（議題 6 必修：先處理 escalate 邊界，對齊 legacy line 290-298）
        // ============================================================

        // adjustment 流程結束 totalRounds += 1（對齊 legacy line 291 totalRounds++）
        state.TotalRounds = state.Round + 1;
        state.MeetingLog  = logSb.ToString();
        await DesignStateHelpers.SaveAsync(context, state);

        if (state.TotalRounds > state.MaxRounds)
        {
            _logger.LogInformation(
                "[Stage52] Adjustment needs_meeting 後超過上限 → escalate（GroupId={Id}，totalRounds={Total}）",
                state.GroupId, state.TotalRounds);

            await context.SendMessageAsync(new DesignPetraVerdict
            {
                Decision       = "escalate",
                Summary        = "多輪調整後仍需重開會議，已達設計會議上限",
                PetraOutput    = petraEvalOutput,
                Round          = state.Round,
                MaxRounds      = state.MaxRounds,
                EscalateReason = "多輪調整後仍需重開會議，已達設計會議上限",
            });
            return;
        }

        _logger.LogInformation(
            "[Stage52] Adjustment needs_meeting → loop back（GroupId={Id}，round={Round} < max={Max}）",
            state.GroupId, state.Round, state.MaxRounds);

        // 重組 verdict 送回 AddSwitch（needs_discussion 走 loop back → DesignRoundStartExecutor）
        // ⚠️ Round 必修（對齊 Stage 51 MidInterruptCheckExecutor.HandleResponseAsync）：用 state.Round（保持 N），
        // 讓下游 DesignRoundStartExecutor.HandleLoopBackAsync 推進 state.Round = verdict.Round + 1 = N + 1
        await context.SendMessageAsync(new DesignPetraVerdict
        {
            Decision    = "needs_discussion",
            Summary     = $"調整後 needs_meeting：{evalDecision?.Reason ?? "需重開會議"}",
            PetraOutput = state.LastPetraOutput ?? petraEvalOutput,
            Round       = state.Round,
            MaxRounds   = state.MaxRounds,
        });
    }
}
