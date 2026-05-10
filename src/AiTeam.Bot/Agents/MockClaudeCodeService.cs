using AiTeam.Bot.Services;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 17：MockMode 模擬 Claude Code subprocess 呼叫，回傳預設結果，不消耗 API。
/// 各方法的 mock output 格式設計為能通過對應 Agent 的 JSON parser，
/// 無法解析時由 Agent fallback 到 MockLlmProvider。
///
/// Stage 32：延遲範圍改由 AppSettings（Mock:DelayMinMs / Mock:DelayMaxMs）動態讀取，
/// 預設 30000-60000 ms，可於 Dashboard 系統設定調整以加速複驗。
/// </summary>
public class MockClaudeCodeService(
    AppSettingsService appSettings,
    ILogger<MockClaudeCodeService> logger) : IClaudeCodeService
{

    /// <summary>
    /// 強制失敗情境（供 /mock fail_* 指令使用）。
    /// 各 Agent 的 MockMode 區塊會依據此值決定是否回傳失敗結果。
    /// 每次使用後由 Agent 自行推進到下一個值（或清為 null）。
    ///
    /// 狀態機：
    ///  fail_review  → ReviewerAgent 設為 review_cody_appeal，回傳 Critical
    ///  review_cody_appeal → ReviewAppealService.RunCodyAppealAsync 設為 review_vera_appeal，Cody disagree
    ///  review_vera_appeal → ReviewAppealService.RunVeraAppealAsync 設為 null，Vera maintain critical
    ///
    ///  qa_failure → QaAgent 設為 null，回傳 failed 報告
    ///
    ///  dev_plan_appeal → PmReviewService.ReviewDevPlanAsync 設為 dev_plan_cody_appeal，回傳 revise
    ///  dev_plan_cody_appeal → DevPlanAppealService.RunCodyDevPlanAppealAsync 設為 null，Cody disagree
    /// </summary>
    public static string? FailScenario { get; set; }

    /// <summary>
    /// Stage 45 Mock 場景：模擬「外部按下暫停」的觸發點。
    /// FireStepsAsync 進入時若偵測到 (groupId, beforeStep) 匹配，會將 group 標為 IsPaused = true，
    /// 由後續 IsPaused 閘門攔下（等同 Christ 從 Dashboard 按暫停的時序）。一次性，觸發後自動清為 null。
    /// </summary>
    public static (Guid groupId, string beforeStep)? PausePoint { get; set; }

    /// <summary>Stage 53B：scenario-scoped round counter（per agent 內 round 計數，依 FailScenario + agentName key 區分）。
    /// 用以動態控制 Mock 行為（fix loop / appeal / blocker round 路徑切換）。
    /// MockScenarioService 設 FailScenario 時呼叫 ResetScenarioRoundCounters() 確保新 run 從 Round 1 開始。</summary>
    private static readonly Dictionary<string, int> _scenarioRoundCounters = new();
    private static readonly object _counterLock = new();

    /// <summary>Stage 53B：清空所有 scenario round counter（新 /mock run 觸發前呼叫）。</summary>
    public static void ResetScenarioRoundCounters()
    {
        lock (_counterLock) _scenarioRoundCounters.Clear();
    }

    /// <summary>Stage 53B：取對應 agent 的 round counter（並 increment 給下次用）— 從 1 開始。
    /// 驗收期 follow-up #1：改 public，給 ReviewerAgentService / DevAgentService / QaAgentService 的 MockMode early return 用。</summary>
    public static int GetAndIncrementRound(string agentKey)
    {
        lock (_counterLock)
        {
            var current = _scenarioRoundCounters.GetValueOrDefault(agentKey, 0);
            _scenarioRoundCounters[agentKey] = current + 1;
            return current + 1;
        }
    }

    /// <summary>
    /// 模擬 Dev Agent 完整開發（RunAsync）。
    /// Output 包含 /pull/999，讓 DevAgentService.ExtractPrNumberFromText 可解析 PR 編號。
    /// </summary>
    [Obsolete("Stage 53B+：Mock 已搬到 DevAgentService MockMode early return（含 dev_blocker_appeal scenario）；保留 method body 作 fallback 文檔與向下相容。")]
    public async Task<ClaudeCodeResult> RunAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunAsync 回傳模擬結果");
        await Task.Delay(await appSettings.GetMockDelayMsAsync(ct), ct);
        // Stage 53B follow-up #1：dev_blocker_appeal Mock branch 已搬到 DevAgentService.cs MockMode early return 內
        // （DevAgentService 的 MockMode 不會 call 本 method）
        const string output = "[MOCK] 開發完成，程式碼已實作並通過 build\nhttps://github.com/mock/repo/pull/999";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬唯讀探索（RunReadOnlyAsync）。
    /// Output 為 JSON 陣列格式，供 Rosa 的 TryParseIssues 解析。
    /// Petra 的 TryParseReview 因找不到 "decision" 欄位會回傳 null，fallback 到 MockLlmProvider。
    /// Demi / Dev 直接取 Output 字串使用，不影響功能。
    /// </summary>
    public async Task<ClaudeCodeResult> RunReadOnlyAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunReadOnlyAsync 回傳模擬結果");
        await Task.Delay(await appSettings.GetMockDelayMsAsync(ct), ct);

        const string output =
            "[MOCK] 探索完成\n" +
            "[{\"title\":\"[MOCK] 模擬需求功能\",\"body\":\"這是 Mock Mode 產生的模擬需求，用於測試流程。\",\"labels\":[\"enhancement\"]}]";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬 Victoria CEO 模式（RunVictoriaAsync）。
    /// Output 包含 &lt;ACTION&gt; 區塊，供 CeoAgentService.TryParseActionBlock 解析為 CeoResponse。
    /// </summary>
    public async Task<ClaudeCodeResult> RunVictoriaAsync(
        string workingDir, string prompt, string model, string anthropicApiKey,
        IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunVictoriaAsync 回傳模擬結果");
        await Task.Delay(await appSettings.GetMockDelayMsAsync(ct), ct);
        const string output =
            "[MOCK] Victoria 分析完成\n" +
            "<ACTION>{\"action\":\"reply\",\"reply\":\"[MOCK] Victoria 已完成分析，這是模擬模式回應。\",\"require_confirmation\":false,\"docs_committed\":false}</ACTION>";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬 QA 測試產生（RunQaAsync）。
    /// Output 為 QaReport JSON，供 QaAgentService.TryParseQaReport 解析。
    /// </summary>
    [Obsolete("Stage 53B+：Mock 已搬到 QaAgentService MockMode early return（含 qa_no_tests_dynamic scenario）；保留 method body 作 fallback 文檔與向下相容。")]
    public async Task<ClaudeCodeResult> RunQaAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunQaAsync 回傳模擬結果");
        await Task.Delay(await appSettings.GetMockDelayMsAsync(ct), ct);
        // Stage 53B follow-up #1：qa_no_tests_dynamic Mock branch 已搬到 QaAgentService.cs MockMode early return 內
        const string output =
            "[MOCK] QA 完成\n" +
            "{\"generated\":[\"[MOCK] MockFeatureTest.cs\"],\"summary\":\"[MOCK] QA 測試通過，0 個失敗\"}";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬 Code Review（RunReviewAsync）。
    /// Output 為 ReviewReport JSON，供 ReviewerAgentService.TryParseReviewReport 解析。
    /// </summary>
    [Obsolete("Stage 53B+：Mock 已搬到 ReviewerAgentService MockMode early return（含 fix loop / reviewer_fallback / max_iter / crash_recovery scenarios）；保留 method body 作 fallback 文檔與向下相容。")]
    public async Task<ClaudeCodeResult> RunReviewAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunReviewAsync 回傳模擬結果");
        await Task.Delay(await appSettings.GetMockDelayMsAsync(ct), ct);
        // Stage 53B follow-up #1：fix loop / reviewer_fallback / max_iter / crash_recovery Mock branch 已搬到 ReviewerAgentService.cs MockMode early return 內
        const string output =
            "[MOCK] 審查完成\n" +
            "{\"critical\":[],\"warning\":[],\"info\":[],\"summary\":\"[MOCK] 模擬審查通過，程式碼品質符合要求\",\"impact\":\"[MOCK] 無影響範圍\"}";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// Stage 25a：模擬持續對話 session（RunMeetingSessionAsync）。
    /// 依 sessionId 後綴判斷 Agent 角色，回傳對應的 mock 意見。
    /// Petra 的回應結尾含合法 JSON，確保 MockMode 下自動達成 consensus，不卡流程。
    /// </summary>
    public async Task<ClaudeCodeResult> RunMeetingSessionAsync(
        string workingDir, string sessionId, string prompt, string model, string anthropicApiKey,
        bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MockMode] MockClaudeCodeService.RunMeetingSessionAsync（sessionId={Id}，isFirst={IsFirst}）",
            sessionId, isFirstMessage);
        await Task.Delay(await appSettings.GetMockDelayMsAsync(ct), ct);

        // Stage 60-FF 五十五：meeting subprocess failure mock injection
        // 機制：FailScenario="meeting_subprocess_failure" → RunMeetingSessionAsync 開頭回 Success=false → MeetingCommons.RunAgentTurnAsync 改 throw
        // MeetingSubprocessFailureException → KickoffStageExecutor 外圍 catch → fire agent_api_failure_intervention agent="Petra-Kickoff"。
        // 一次性消耗（throw 後重設 null）— 對齊 split_task_subtask_fail_intervention 等 mock 場景模式，避免 retry path 重複觸發。
        if (FailScenario == "meeting_subprocess_failure")
        {
            logger.LogWarning("[MockMode][Stage60] meeting_subprocess_failure 觸發 — 回 Success=false 模擬 subprocess 失敗（一次性，sessionId={Id}）", sessionId);
            FailScenario = null;
            return new ClaudeCodeResult(false, "[MOCK] subprocess failure simulated（meeting_subprocess_failure）", 0, "");
        }
        // Stage 60：modify path subprocess 失敗 — 只在 modify prompt 觸發（prompt 含「老闆要求修改」）
        // 對應場景 D meeting_modify_during_subprocess_failure：modify happy 走完後 inject failure
        if (FailScenario == "meeting_modify_during_subprocess_failure"
            && prompt.Contains("老闆要求修改"))
        {
            logger.LogWarning("[MockMode][Stage60] meeting_modify_during_subprocess_failure 觸發 — modify path 回 Success=false（一次性，sessionId={Id}）", sessionId);
            FailScenario = null;
            return new ClaudeCodeResult(false, "[MOCK] modify subprocess failure simulated", 0, "");
        }
        // Stage 60：framework_modify_taskplan_happy / framework_modify_designplan_happy
        // 觸發點 — modify prompt（prompt 含「老闆要求修改」）回完整 modify result（≥ 4000 字）
        if ((FailScenario == "framework_modify_taskplan_happy" || FailScenario == "framework_modify_designplan_happy")
            && prompt.Contains("老闆要求修改"))
        {
            logger.LogInformation("[MockMode][Stage60] {Scenario} modify path 回完整 modify result（sessionId={Id}）", FailScenario, sessionId);
            // 一次性消耗（modify 完成後 BossInteraction 重開，不再觸發此分支）
            FailScenario = null;
            // build ≥ 4000 字 mock revised plan 文字（驗 DB TaskPlan / DesignPlan ≥ 4000）
            var bigPlan = new System.Text.StringBuilder();
            bigPlan.AppendLine("# [MOCK] Petra 修改後計劃書（Stage 60 framework modify path）");
            bigPlan.AppendLine();
            bigPlan.AppendLine("根據老闆修改意見全面更新如下：");
            bigPlan.AppendLine();
            for (var i = 1; i <= 80; i++)
            {
                bigPlan.AppendLine($"## 章節 {i}：詳細內容說明（mock）");
                bigPlan.AppendLine($"這是 Stage 60 mock 修改 Round 後的第 {i} 章節，涵蓋完整變更說明、影響面分析、執行策略、風險評估。對齊 framework Pipeline modify lifecycle。");
                bigPlan.AppendLine();
            }
            var revisedPlan = bigPlan.ToString().Replace("\"", "\\\"").Replace("\n", "\\n");
            var modifyOutput = "[MOCK] Petra modify 完成（Stage 60 framework path）\n" +
                $"{{\"impact\":\"small\",\"revised_plan\":\"{revisedPlan}\"}}";
            return new ClaudeCodeResult(true, modifyOutput, 0, "");
        }

        // Stage 30：申訴環節 mock 分支（優先於 agentName 判斷）
        // FailScenario 失敗路徑已在 Pm/ 各 service 早返回，不會到達此處
        //
        // 設計：Mock 分支依 prompt 內容動態判斷輪次，確保 fail_* 場景能走完
        // 整個迴圈，觸發 maxRounds 上限的仲裁 / 重評路徑，覆蓋所有新 CLI 分支。
        if (prompt.Contains("[APPEAL:review_cody]"))
        {
            // Round 2+ 的 Cody Appeal prompt 含「前幾輪對話紀錄」（priorContext），此時持續 disagree
            // 逼到 maxRounds 上限觸發 Petra 仲裁；Round 1 永不走此分支（FailScenario 早返回）。
            if (prompt.Contains("前幾輪對話紀錄"))
                return new ClaudeCodeResult(true,
                    "{\"items\":[{\"id\":1,\"response\":\"disagree\",\"reason\":\"[MOCK] 仍持反對意見，逼到 maxRounds 觸發 Petra 仲裁\"}]}", 0, "");
            return new ClaudeCodeResult(true,
                "{\"items\":[{\"id\":1,\"response\":\"agree\",\"reason\":\"[MOCK] 同意修正\"}]}", 0, "");
        }
        if (prompt.Contains("[APPEAL:review_vera]"))
        {
            // Cody 持續 disagree 時維持 Critical（搭配 review_cody 的 Round 2+ 邏輯，讓仲裁被觸發）
            if (prompt.Contains("\"disagree\""))
                return new ClaudeCodeResult(true,
                    "{\"accepted_ids\":[],\"maintained_ids\":[1],\"updated_summary\":\"[MOCK] Vera 維持 Critical，不接受 Cody 反駁\"}", 0, "");
            return new ClaudeCodeResult(true,
                "{\"accepted_ids\":[],\"maintained_ids\":[],\"updated_summary\":\"[MOCK] Vera 接受 Cody 反駁，全數撤銷 Critical\"}", 0, "");
        }
        if (prompt.Contains("[APPEAL:review_arbitration]"))
            return new ClaudeCodeResult(true,
                "{\"decision\":\"support_cody_full\",\"final_criticals\":[],\"reasoning\":\"[MOCK] 支持 Cody 反駁，Critical 全數撤銷\"}", 0, "");
        if (prompt.Contains("[APPEAL:dev_plan_cody]"))
            return new ClaudeCodeResult(true,
                "{\"position\":\"accept\",\"reasoning\":\"[MOCK] 接受 Petra 意見，依建議修正\"}", 0, "");
        if (prompt.Contains("[APPEAL:dev_plan_petra]"))
        {
            // Round 1 的 previousReview 帶有 Petra 初審的 MOCK-FAIL 關鍵字 → 回 revise，推進到 Round 2
            // 讓 Cody Dev_plan CLI 分支也被觸發；Round 2+ 的 previousReview 不含此字樣 → approve 結束
            if (prompt.Contains("[MOCK-FAIL] Dev_plan 不夠詳細"))
                return new ClaudeCodeResult(true,
                    "{\"decision\":\"revise\",\"summary\":\"[MOCK] 維持修改意見，請 Cody 再次評估\",\"issues\":[{\"severity\":\"blocking\",\"description\":\"[MOCK] 計劃仍需補充\"}],\"revision_instructions\":\"[MOCK] 請再想想\"}", 0, "");
            return new ClaudeCodeResult(true,
                "{\"decision\":\"approve\",\"summary\":\"[MOCK] 接受 Cody 反駁，核准 Dev_plan\",\"issues\":[],\"revision_instructions\":null}", 0, "");
        }

        // Stage 46-FF 三十五：Rosa Design 階段拆 12 Issues（觸發規則層 IssueCount ≥ 8）
        // 必須在 RunMeetingSessionAsync 內偵測，因為 Rosa 走 meetingCommons.RunAgentTurnAsync 不是 RunReadOnlyAsync。
        // 用 prompt 特徵（"你是 Rosa" + "設計前置作業"）+ FailScenario split_task_* 雙條件判斷。
        // output 不能 [MOCK] 前綴開頭，TryParseDesignIssues 用 IndexOf('[') 會被 [MOCK] 絆倒（Stage 24 級歷史包袱觀察）。
        if (FailScenario is "split_task_propose_accept" or "split_task_subtask_fail_intervention"
            && prompt.Contains("你是 Rosa") && prompt.Contains("設計前置作業"))
        {
            const string rosaSplitOutput =
                "MOCK 探索完成（拆 task 場景：12 Issues）\n" +
                "[" +
                "{\"title\":\"MOCK Issue 1 schema migration\",\"body\":\"基礎 schema\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 2 base service\",\"body\":\"共用基礎\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 3 component A\",\"body\":\"元件遷移\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 4 component B\",\"body\":\"元件遷移\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 5 component C\",\"body\":\"元件遷移\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 6 component D\",\"body\":\"元件遷移\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 7 component E\",\"body\":\"元件遷移\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 8 component F\",\"body\":\"元件遷移\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 9 component G\",\"body\":\"元件遷移\",\"labels\":[\"feature\"]}," +
                "{\"title\":\"MOCK Issue 10 docs\",\"body\":\"收尾文件\",\"labels\":[\"docs\"]}," +
                "{\"title\":\"MOCK Issue 11 tests\",\"body\":\"收尾測試\",\"labels\":[\"test\"]}," +
                "{\"title\":\"MOCK Issue 12 a11y polish\",\"body\":\"收尾 a11y\",\"labels\":[\"polish\"]}" +
                "]";
            return new ClaudeCodeResult(true, rosaSplitOutput, 0, "");
        }

        // Stage 46-FF 三十五：[SPLIT-TASK] prompt 分支（Petra 拆 task 提案）
        if (prompt.Contains("[SPLIT-TASK]"))
        {
            // 兩個 split task 場景都回相同 phases JSON（差別在後續 sub-task 是否失敗）
            const string phasesJson =
                "{\"should_split\":true,\"rationale\":\"[MOCK] 12 Issue 跨基礎/遷移/收尾三階段，建議拆 3 個 sub-task\",\"phases\":[" +
                "{\"phase\":1,\"description\":\"基礎結構\",\"issues\":[1,2],\"estimated_minutes\":30}," +
                "{\"phase\":2,\"description\":\"元件遷移\",\"issues\":[3,4,5,6,7,8,9],\"estimated_minutes\":120}," +
                "{\"phase\":3,\"description\":\"收尾驗收\",\"issues\":[10,11,12],\"estimated_minutes\":60}" +
                "]}";
            return new ClaudeCodeResult(true, "[MOCK] Petra 拆 task 提案\n" + phasesJson, 0, "");
        }

        // Stage 26：改用 prompt 內容判斷角色（各 prompt builder 均以「你是 {Name}，」開頭）
        // 原本用 sessionId.Split('-').Last() 無法正確匹配純 UUID 格式的 session ID
        // Stage 50：BuildPetraPlanPrompt（KickoffPlanExecutor 用）無「你是 Petra」字樣，補識別 "Kick-off 會議已結束" 特徵字串
        // Stage 52 驗收期 follow-up #1：DesignPrompts.BuildDesignPetraPlanPrompt 開頭「設計會議已結束」+ DesignAdjustmentExecutor
        // evalSb「Rosa 和 Demi 已完成修改」兩字串無「你是 Petra」開頭 → 補識別（對齊 Stage 50 踩坑 #11 教訓延續）
        var agentName = prompt.Contains("你是 Petra")
                     || prompt.Contains("Kick-off 會議已結束")
                     || prompt.Contains("設計會議已結束")
                     || prompt.Contains("Rosa 和 Demi 已完成修改") ? "petra"
                      : prompt.Contains("你是 Rosa")  ? "rosa"
                      : prompt.Contains("你是 Demi")  ? "demi"
                      : prompt.Contains("你是 Cody")  ? "cody"
                      : prompt.Contains("你是 Quinn") ? "quinn"
                      : "unknown";

        // Stage 50：framework Kickoff Meeting 5 場景下 Petra 依 round 切換 decision
        // prompt 包含 "## 第 N 輪各角色意見"（KickoffPrompts.BuildPetraRoundPrompt 格式）— 以此判 Round
        // 注意：場景 C crash_recovery 的 PausePoint 機制不適用 Kickoff（Kickoff 不走 dispatcher fire steps），
        // 改採「Round 1+2 needs_discussion 推進 framework Workflow Round 2」+ Christ 線下 docker restart 驗 Recovery
        if (agentName == "petra" && FailScenario is "framework_kickoff_consensus_round1"
                                                or "framework_kickoff_consensus_round2"
                                                or "framework_kickoff_max_iter"
                                                or "framework_kickoff_escalate"
                                                or "framework_kickoff_crash_recovery"
                                                // Stage 51 v4 漸進遷移第三步：HITL 中途介入 4 場景
                                                or "framework_kickoff_mid_interrupt_apply"
                                                or "framework_kickoff_mid_interrupt_cancel"
                                                or "framework_kickoff_mid_interrupt_crash_during_wait"
                                                or "framework_kickoff_mid_interrupt_no_trigger")
        {
            var round = prompt.Contains("## 第 1 輪各角色意見") ? 1
                      : prompt.Contains("## 第 2 輪各角色意見") ? 2
                      : prompt.Contains("## 第 3 輪各角色意見") ? 3
                      : 0;  // 0 = 非 round prompt（如 BuildPetraPlanPrompt 產出最終計劃書）

            // round == 0 → BuildPetraPlanPrompt（KickoffPlanExecutor 觸發）→ 回 Markdown 計劃書，無 decision JSON
            if (round == 0)
            {
                return new ClaudeCodeResult(true,
                    "# 任務計劃書\n\n## 任務摘要\n[MOCK] framework Kickoff 路徑產出之任務計劃書。\n\n" +
                    "## 關鍵決策\n- [MOCK] 5 Agent 達成共識（或 max_iter 強制結束）\n\n" +
                    "## 各角色意見摘要\n| 角色 | 主要意見 | 結論 |\n|------|---------|------|\n" +
                    "| Rosa | [MOCK] 需求清晰 | 已確認 |\n" +
                    "| Demi | [MOCK] UI 可容納 | 已確認 |\n" +
                    "| Cody | [MOCK] 技術可行 | 已確認 |\n" +
                    "| Quinn | [MOCK] 可測試 | 已確認 |\n\n" +
                    "## 風險與注意事項\n- [MOCK] 無重大風險\n\n" +
                    "## 建議實作方向\n[MOCK] 沿用既有架構。", 0, "");
            }

            // round 1-3：依 scenario 切 decision
            var decision = (FailScenario, round) switch
            {
                ("framework_kickoff_consensus_round1", _)              => "consensus",
                ("framework_kickoff_consensus_round2", 1)              => "needs_discussion",
                ("framework_kickoff_consensus_round2", _)              => "consensus",
                ("framework_kickoff_escalate", _)                      => "escalate",
                ("framework_kickoff_max_iter", _)                      => "needs_discussion",   // 全部 needs_discussion，Round >= MaxRounds 時 Switch 走 max_iter 路徑
                ("framework_kickoff_crash_recovery", _)                => "needs_discussion",   // Round 1+2 推進，Christ 線下 restart 觀察 Recovery
                // Stage 51 HITL 試點：apply / cancel / crash_during_wait Round 1 needs_discussion → Round 2 consensus
                // （Round 1 結束後 MidInterruptCheckExecutor 看到預設 trigger flag → emit RequestInfoEvent → 等 Christ 回應 resume）
                ("framework_kickoff_mid_interrupt_apply", 1)             => "needs_discussion",
                ("framework_kickoff_mid_interrupt_apply", _)             => "consensus",
                ("framework_kickoff_mid_interrupt_cancel", 1)            => "needs_discussion",
                ("framework_kickoff_mid_interrupt_cancel", _)            => "consensus",
                ("framework_kickoff_mid_interrupt_crash_during_wait", 1) => "needs_discussion",
                ("framework_kickoff_mid_interrupt_crash_during_wait", _) => "consensus",
                // no_trigger：所有 round 都 consensus（baseline 驗證試點 flag 不影響 default behavior）
                ("framework_kickoff_mid_interrupt_no_trigger", _)        => "consensus",
                _                                                       => "consensus",
            };
            var summaryText = decision switch
            {
                "consensus"        => "[MOCK] framework Kickoff 達成共識",
                "needs_discussion" => "[MOCK] framework Kickoff 需進一步討論",
                "escalate"         => "[MOCK] framework Kickoff 偵測到無法團隊內解決的分歧，上呈老闆裁決",
                _                  => "[MOCK]"
            };
            return new ClaudeCodeResult(true,
                $"[MOCK] Petra Round {round} 整理完成（framework path / scenario={FailScenario}）。\n" +
                "{\"decision\":\"" + decision + "\",\"summary\":\"" + summaryText + "\",\"discussion_points\":[]}", 0, "");
        }

        // ============================================================
        // Stage 52：framework Design Meeting 6 場景 Mock 邏輯（v4 漸進遷移第四步）
        // 識別 prompt 特徵：
        //   - PetraJudge：「正在判斷設計階段是否需要 Demi 參與」
        //   - Petra round（Design）：「正在主持設計會議第 N 輪」+ 「## 第 N 輪各角色意見」
        //   - Petra adjustment eval：「Rosa 和 Demi 已完成修改」
        //   - Petra plan（Design）：「設計會議已結束」+「產出設計規劃書」
        //   - Rosa pre-work：「你是 Rosa」+「正在進行設計前置作業」
        //   - Demi pre-work：「你是 Demi」+「正在進行設計前置作業」
        //   - Rosa adjust：「請調整你的 GitHub Issues」
        //   - Demi adjust：「請調整你的 UI 規格」
        //   - Cody/Quinn 主迴圈：「你是 Cody」/「你是 Quinn」+「正在參加設計會議」→ default agentName switch 處理（line 末段）
        // ============================================================
        var isFrameworkDesignScenario = FailScenario is "framework_design_consensus_round1"
                                                      or "framework_design_consensus_round2"
                                                      or "framework_design_needs_adjustment_approved"
                                                      or "framework_design_needs_adjustment_needs_meeting"
                                                      or "framework_design_no_demi"
                                                      or "framework_design_crash_recovery_during_round";

        if (isFrameworkDesignScenario)
        {
            // PetraJudge：needs_demi 判斷
            if (agentName == "petra" && prompt.Contains("正在判斷設計階段是否需要 Demi 參與"))
            {
                var needsDemi = FailScenario != "framework_design_no_demi";
                return new ClaudeCodeResult(true,
                    $"[MOCK] Petra needsDemi 判斷完成（scenario={FailScenario}）。\n" +
                    $"{{\"needs_demi\":{needsDemi.ToString().ToLower()},\"reason\":\"[MOCK] judge based on scenario\"}}",
                    0, "");
            }

            // Petra adjustment eval（DesignAdjustmentExecutor 內）
            if (agentName == "petra" && prompt.Contains("Rosa 和 Demi 已完成修改"))
            {
                var evaluation = FailScenario == "framework_design_needs_adjustment_needs_meeting"
                               ? "needs_meeting" : "approved";
                // approved 路徑帶 design_plan（議題 7 主路徑驗證 — DesignPlanExecutor 直接 wrap 不再 LLM call）
                var planJson = evaluation == "approved"
                    ? "[MOCK] 設計規劃書（adjustment approved 直接帶 plan，DesignPlanExecutor 直接 wrap）"
                    : "";
                return new ClaudeCodeResult(true,
                    $"[MOCK] Petra 評估調整完成（scenario={FailScenario}）。\n" +
                    $"{{\"evaluation\":\"{evaluation}\",\"design_plan\":\"{planJson}\",\"reason\":\"[MOCK] eval\"}}",
                    0, "");
            }

            // Petra plan（DesignPlanExecutor.HandleVerdictAsync consensus / max_iter 入口）
            if (agentName == "petra" && prompt.Contains("設計會議已結束"))
            {
                return new ClaudeCodeResult(true,
                    "# 設計規劃書\n\n## 需求摘要\n[MOCK] framework Design 路徑產出設計規劃書（< 500 行 + 0 phase 標記，不觸發 split proposal）\n\n" +
                    "## GitHub Issues 清單\n| # | Issue | 標題 | 說明 |\n|---|-------|------|------|\n" +
                    "| 1 | mock-1 | [MOCK] Issue 1 | mock |\n\n" +
                    "## 設計決策\n- [MOCK] 設計會議達成共識\n\n" +
                    "## 風險與注意事項\n- [MOCK] 無重大風險\n\n" +
                    "## 開發建議\n[MOCK] 沿用既有架構。",
                    0, "");
            }

            // Petra round（Design 主迴圈，prompt 含 "## 第 N 輪各角色意見"）
            if (agentName == "petra" && prompt.Contains("正在主持設計會議第"))
            {
                var round = prompt.Contains("## 第 1 輪各角色意見") ? 1
                          : prompt.Contains("## 第 2 輪各角色意見") ? 2
                          : prompt.Contains("## 第 3 輪各角色意見") ? 3
                          : 1;

                var decision = (FailScenario, round) switch
                {
                    ("framework_design_consensus_round1", _)               => "consensus",
                    ("framework_design_consensus_round2", 1)               => "needs_discussion",
                    ("framework_design_consensus_round2", _)               => "consensus",
                    ("framework_design_needs_adjustment_approved", 1)      => "needs_adjustment",
                    ("framework_design_needs_adjustment_approved", _)      => "consensus",        // 備援（approved 路徑不會跑到 Round 2）
                    ("framework_design_needs_adjustment_needs_meeting", 1) => "needs_adjustment",
                    ("framework_design_needs_adjustment_needs_meeting", _) => "consensus",        // 外層 loop back round+1 後 consensus
                    ("framework_design_no_demi", _)                        => "consensus",
                    ("framework_design_crash_recovery_during_round", _)    => "needs_discussion", // Round 1+2 推進，Christ 線下 restart 觀察 Recovery
                    _                                                      => "consensus",
                };

                // needs_adjustment 路徑需帶 adjustment_targets / adjustment_instructions（DesignAdjustmentExecutor 解析用）
                var (targetsJson, instructionsJson) = decision == "needs_adjustment"
                    ? ("[\"rosa\",\"demi\"]",
                       "{\"rosa\":\"[MOCK] 請調整 Issue 描述更具體\",\"demi\":\"[MOCK] 請調整 UI 規格細節\"}")
                    : ("[]", "{}");

                var summaryText = decision switch
                {
                    "consensus"        => "[MOCK] 設計會議達成共識",
                    "needs_discussion" => "[MOCK] 設計會議需進一步討論",
                    "needs_adjustment" => "[MOCK] Petra 要求 Rosa/Demi 調整",
                    "escalate"         => "[MOCK] 設計會議偵測到無法團隊內解決的分歧",
                    _                  => "[MOCK]"
                };

                return new ClaudeCodeResult(true,
                    $"[MOCK] Petra Round {round} 整理完成（framework Design path / scenario={FailScenario}）。\n" +
                    $"{{\"decision\":\"{decision}\",\"summary\":\"{summaryText}\",\"adjustment_targets\":{targetsJson},\"adjustment_instructions\":{instructionsJson},\"escalate_reason\":\"\"}}",
                    0, "");
            }

            // Rosa pre-work（Design 階段，3 個 Issue，issuesJson < 8 不觸發 split proposal 規則層）
            if (agentName == "rosa" && prompt.Contains("正在進行設計前置作業"))
            {
                return new ClaudeCodeResult(true,
                    "MOCK Rosa Design pre-work 完成（3 Issues，不觸發拆 task）\n" +
                    "[" +
                    "{\"title\":\"MOCK Design Issue 1\",\"body\":\"模擬 Issue 1\",\"labels\":[\"feature\"]}," +
                    "{\"title\":\"MOCK Design Issue 2\",\"body\":\"模擬 Issue 2\",\"labels\":[\"feature\"]}," +
                    "{\"title\":\"MOCK Design Issue 3\",\"body\":\"模擬 Issue 3\",\"labels\":[\"feature\"]}" +
                    "]",
                    0, "");
            }

            // Demi pre-work（Design 階段）
            if (agentName == "demi" && prompt.Contains("正在進行設計前置作業"))
            {
                return new ClaudeCodeResult(true,
                    "[MOCK] Demi UI/UX 規格\n## 頁面結構\n- 元件 A\n## 元件清單\n- MudButton\n## 互動說明\n- 點擊觸發\n## MudBlazor 元件建議\n- MudStack",
                    0, "");
            }

            // Rosa adjust（DesignAdjustmentExecutor 內）
            if (agentName == "rosa" && prompt.Contains("請調整你的 GitHub Issues"))
            {
                return new ClaudeCodeResult(true,
                    "MOCK Rosa adjust 完成\n" +
                    "[" +
                    "{\"title\":\"MOCK Design Issue 1 (adjusted)\",\"body\":\"adjusted\",\"labels\":[\"feature\"]}," +
                    "{\"title\":\"MOCK Design Issue 2 (adjusted)\",\"body\":\"adjusted\",\"labels\":[\"feature\"]}," +
                    "{\"title\":\"MOCK Design Issue 3 (adjusted)\",\"body\":\"adjusted\",\"labels\":[\"feature\"]}" +
                    "]",
                    0, "");
            }

            // Demi adjust（DesignAdjustmentExecutor 內）
            if (agentName == "demi" && prompt.Contains("請調整你的 UI 規格"))
            {
                return new ClaudeCodeResult(true,
                    "[MOCK] Demi adjust 完成\n## 頁面結構\n- 元件 A v2\n## 元件清單\n- MudButton (updated)",
                    0, "");
            }

            // Rosa Design 主迴圈 / Demi Design 主迴圈 / Cody Design 主迴圈 / Quinn Design 主迴圈：
            // fall through 到下方 default agentName switch（mock 各自意見）
        }

        var output = agentName switch
        {
            "petra" =>
                "[MOCK] Petra 整理完成，所有 Agent 意見已彙整，沒有重大分歧。\n" +
                "{\"decision\":\"consensus\",\"summary\":\"[MOCK] 會議順利完成，各角色無重大疑慮。\",\"discussion_points\":[]}",
            "rosa" =>
                "[MOCK] Rosa 需求分析完成。需求描述清晰，無模糊之處。建議在實作前確認 API 設計細節。",
            "demi" =>
                "[MOCK] Demi UI/UX 評估完成。現有 Dashboard 結構可容納此功能，無需大規模 Layout 調整。",
            "cody" =>
                "[MOCK] Cody 技術可行性評估完成。技術上可行，現有架構支援此功能，預計 2 天完成。",
            "quinn" =>
                "[MOCK] Quinn 測試規劃完成。此功能可自動化測試，建議加入 E2E 截圖驗證。",
            _ =>
                "[MOCK] 會議發言完成。"
        };

        return new ClaudeCodeResult(true, output, 0, "");
    }
}
