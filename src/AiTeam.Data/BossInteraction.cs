namespace AiTeam.Data;

/// <summary>
/// Stage 28a：老闆互動紀錄。
/// 每次 Bot 需要 Christ 確認時建立一筆，支援 Discord 和 Dashboard 雙通道先到先贏回覆。
/// </summary>
public class BossInteraction
{
    public Guid     Id                   { get; set; }
    public Guid?    TaskGroupId          { get; set; }
    public Guid?    TaskItemId           { get; set; }

    /// <summary>互動類型：ceo_confirm / exec_confirm / proposal / kickoff / design / devplan_escalate / merge_notify / intervention</summary>
    public string   InteractionType      { get; set; } = "";

    /// <summary>狀態：pending / responded / expired</summary>
    public string   Status               { get; set; } = "pending";

    /// <summary>Dashboard 列表摘要標題。</summary>
    public string   Title                { get; set; } = "";

    /// <summary>Dashboard 展開顯示的詳細說明。</summary>
    public string   Description          { get; set; } = "";

    public string?  Project              { get; set; }
    public string?  AgentName            { get; set; }

    /// <summary>可用按鈕動作 JSON 陣列（InteractionActionDto[]），純通知類型為 "[]"。</summary>
    public string   AvailableActionsJson { get; set; } = "[]";

    /// <summary>回覆的動作 ID（如 confirm_yes）。</summary>
    public string?  ResponseAction       { get; set; }

    /// <summary>回覆來源：discord / dashboard。</summary>
    public string?  ResponseSource       { get; set; }

    /// <summary>Stage 28b：文字輸入類回覆的內容（修改意見）。</summary>
    public string?  ResponseContent      { get; set; }

    public DateTime? RespondedAt         { get; set; }

    /// <summary>對應的 Discord 訊息 ID（ulong 存為 numeric(20,0)），用於 Discord 回覆時反查。</summary>
    public decimal?  DiscordMessageId    { get; set; }

    /// <summary>互動類型特有上下文 JSON（一律含 channelId）。</summary>
    public string?  ContextJson          { get; set; }

    public DateTime CreatedAt            { get; set; } = DateTime.UtcNow;

    /// <summary>預留欄位，Stage 28a 不設定，Phase 2 實作過期清理。</summary>
    public DateTime? ExpiresAt           { get; set; }

    /// <summary>InteractionProcessor 已消費標記，防止重複處理。</summary>
    public bool     ProcessedByBot       { get; set; } = false;

    public TaskGroup? TaskGroup          { get; set; }
    public TaskItem?  TaskItem           { get; set; }
}
