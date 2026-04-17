namespace AiTeam.Shared.Dtos;

/// <summary>Stage 28a：互動按鈕動作定義。Stage 28b 新增 RequiresInput。</summary>
public record InteractionActionDto(string Id, string Label, string Color, bool RequiresInput = false);

/// <summary>Stage 28a：老闆互動 DTO（Dashboard 列表與詳情顯示用）。</summary>
public record BossInteractionDto
{
    public Guid      Id                  { get; init; }
    public Guid?     TaskGroupId         { get; init; }
    public Guid?     TaskItemId          { get; init; }
    public string    InteractionType     { get; init; } = "";
    public string    Status              { get; init; } = "";
    public string    Title               { get; init; } = "";
    public string    Description         { get; init; } = "";
    public string?   Project             { get; init; }
    public string?   AgentName           { get; init; }
    public List<InteractionActionDto> AvailableActions { get; init; } = [];
    public string?   ResponseAction      { get; init; }
    public string?   ResponseSource      { get; init; }
    /// <summary>Stage 28b：文字輸入類回覆的內容（修改意見）。</summary>
    public string?   ResponseContent     { get; init; }
    public DateTime? RespondedAt         { get; init; }
    public DateTime  CreatedAt           { get; init; }
}

/// <summary>Stage 28a：Dashboard 回覆 API 的 Request Body。Stage 28b 新增 Content。</summary>
public record InteractionResponseRequest(string Action, string? Content = null);
