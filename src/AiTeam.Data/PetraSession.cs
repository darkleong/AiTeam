namespace AiTeam.Data;

/// <summary>
/// Stage 63B：Petra Orchestrator per-task session（v5 動態架構 PoC）。
/// 每個 TaskGroup 啟動 v5 path 時建立一筆，記錄 Petra LLM 動態決策 + Worker dispatch 軌跡。
/// </summary>
public class PetraSession
{
    public Guid Id { get; set; }

    /// <summary>Stage 63B PoC：nullable 允許 spike forward path 無 TaskGroup（Stage 64+ 全量整合時必填）。</summary>
    public Guid? TaskGroupId { get; set; }

    /// <summary>狀態：running / escalated / done / paused（Stage 80 HITL plan_confirm 等待）/ cancelled（Stage 80 reject）。
    /// Stage 81 動態 replan 重用既有 paused status — replan_confirm pause 期間 session 同樣不被 PetraSessionRecoveryService 掃描。</summary>
    public string Status { get; set; } = "running";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Stage 81：累積 replan iteration 輪數（approve / edit / respond 後 +1 / reject 不算）。對齊 Workflow:MaxReplanIterations cap。</summary>
    public int ReplanIteration { get; set; } = 0;

    /// <summary>Stage 81：accumulated session cost USD（每個 worker dispatch 完成後從 token_logs WHERE PetraSessionId=... 累計）。
    /// numeric(18,6) 對齊既有 TokenLog.TotalCostUsd 精度。對齊 Workflow:ReplanCostCapUsd soft cap。</summary>
    public decimal SessionCostUsd { get; set; } = 0m;

    /// <summary>Stage 83 v5 Bug 4：v5.5 task 結束 FinalizeGitAsync OpenPullRequestAsync 真實 return 的 GitHub PR URL（nullable — 無 PR 場景 / 純 Q&A task / Mock workingDir 非 git repo 場景為 null）。
    /// PetraOrchestratorService.CompleteAsync caller line 196 寫入 / Dashboard Tasks 歷史 tab 顯示 PR link。</summary>
    public string? ResultPrUrl { get; set; }

    public TaskGroup? TaskGroup { get; set; }
    public List<PetraSessionMessage> Messages { get; set; } = new();
}
