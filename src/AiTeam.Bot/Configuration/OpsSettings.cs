namespace AiTeam.Bot.Configuration;

public class OpsSettings
{
    public int HealthCheckIntervalMinutes { get; set; } = 30;
    public int CpuAlertThresholdPercent { get; set; } = 80;
    public int MemoryAlertThresholdPercent { get; set; } = 80;
}
