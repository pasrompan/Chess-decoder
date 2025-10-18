namespace ChessDecoderApi.DTOs.Responses;

/// <summary>
/// Response model for paginated game lists
/// </summary>
public class GameListResponse
{
    public List<GameSummaryDto> Games { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class GameSummaryDto
{
    public Guid GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public bool IsValid { get; set; }
    public int TotalMoves { get; set; }
    public string? Opening { get; set; }
}

