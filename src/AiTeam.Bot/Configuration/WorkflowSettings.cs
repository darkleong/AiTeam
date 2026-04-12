namespace AiTeam.Bot.Configuration;

/// <summary>Stage 23：工作流程設定，含 Review Appeal 輪次上限與版本號要求。</summary>
public class WorkflowSettings
{
    /// <summary>Cody-Vera Review Appeal 最大輪次（超過後由 Petra 仲裁）。</summary>
    public int ReviewAppealMaxRounds { get; set; } = 3;

    /// <summary>期望的版本號（Vera 版本檢查用）。空白時略過版本檢查。</summary>
    public string TargetVersion { get; set; } = "";
}
