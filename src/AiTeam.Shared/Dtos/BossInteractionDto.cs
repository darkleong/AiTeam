namespace AiTeam.Shared.Dtos;

/// <summary>Stage 28a：互動按鈕動作定義。</summary>
public record InteractionActionDto(string Id, string Label, string Color);

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
    public DateTime? RespondedAt         { get; init; }
    public DateTime  CreatedAt           { get; init; }
}

/// <summary>Stage 28a：Dashboard 回覆 API 的 Request Body。</summary>
public record InteractionResponseRequest(string Action);
