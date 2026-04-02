namespace AiTeam.Bot.Configuration;

public class GitHubSettings
{
    public string PersonalAccessToken { get; set; } = "";
    public string WebhookSecret { get; set; } = "";

    /// <summary>
    /// Dev Agent Clone repo 的本地工作目錄
    /// </summary>
    public string WorkspacePath { get; set; } = @"D:\AiTeam-Workspace";

    /// <summary>
    /// GitHub 帳號名稱（repo owner）
    /// </summary>
    public string Owner { get; set; } = "";

    /// <summary>
    /// 預設 Repo 名稱，直接在 Agent 頻道指派任務時未指定 Project 的 fallback。
    /// </summary>
    public string DefaultRepo { get; set; } = "";
}
