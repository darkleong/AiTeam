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

    /// <summary>狀態：running / escalated / done。</summary>
    public string Status { get; set; } = "running";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TaskGroup? TaskGroup { get; set; }
    public List<PetraSessionMessage> Messages { get; set; } = new();
}
