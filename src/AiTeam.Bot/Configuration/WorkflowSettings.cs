namespace AiTeam.Bot.Configuration;

/// <summary>Stage 23/24：工作流程設定，含 Review Appeal 輪次上限、QA 修復上限與版本號要求。</summary>
public class WorkflowSettings
{
    /// <summary>Cody-Vera Review Appeal 最大輪次（超過後由 Petra 仲裁）。</summary>
    public int ReviewAppealMaxRounds { get; set; } = 3;

    /// <summary>Stage 24：QA 修復迴圈最大輪次（Petra 判斷 code_bug 後，最多觸發幾輪 Dev_fix + QA）。</summary>
    public int QaFixMaxRounds { get; set; } = 3;

    /// <summary>Stage 24：Dev_plan Appeal 最大輪次（Cody-Petra 純 LLM 迴圈，超過後升級給老闆）。</summary>
    public int DevPlanAppealMaxRounds { get; set; } = 3;

    /// <summary>Stage 25a：Kick-off 會議最大輪次（超過後直接請 Petra 產出計劃書）。</summary>
    public int KickoffMaxRounds { get; set; } = 3;

    /// <summary>Stage 25b：設計會議最大輪次（含調整重開的次數，超過後 escalate 給 Christ）。</summary>
    public int DesignMeetingMaxRounds { get; set; } = 3;

    /// <summary>期望的版本號（Vera 版本檢查用）。空白時略過版本檢查。</summary>
    public string TargetVersion { get; set; } = "";
}
