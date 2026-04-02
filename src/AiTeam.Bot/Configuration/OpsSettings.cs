namespace AiTeam.Bot.Configuration;

public class OpsSettings
{
    public int HealthCheckIntervalMinutes { get; set; } = 30;
    public int CpuAlertThresholdPercent { get; set; } = 80;
    public int MemoryAlertThresholdPercent { get; set; } = 80;
    public bool CiCdMonitorEnabled { get; set; } = true;
    public string CiCdWorkflowName { get; set; } = "Build and Deploy";
    /// <summary>幾分鐘內的失敗才視為「最新失敗」需要處理（避免重複通知歷史失敗）。</summary>
    public int CiCdFailureWindowMinutes { get; set; } = 60;
}
