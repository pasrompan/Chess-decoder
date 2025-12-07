using ChessDecoderApi.Models;

namespace ChessDecoderApi.DTOs.Responses;

/// <summary>
/// Response model for detailed game information
/// </summary>
public class GameDetailsResponse
{
    public Guid GameId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PgnContent { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public int ProcessingTimeMs { get; set; }
    public bool IsValid { get; set; }
    public string? ValidationMessage { get; set; }
    public string? WhitePlayer { get; set; }
    public string? BlackPlayer { get; set; }
    public DateTime? GameDate { get; set; }
    public string? Round { get; set; }
    public bool ProcessingCompleted { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public int EditCount { get; set; }
    public GameStatisticsDto? Statistics { get; set; }
    public List<GameImageDto> Images { get; set; } = new();
}

public class GameStatisticsDto
{
    public int TotalMoves { get; set; }
    public int ValidMoves { get; set; }
    public int InvalidMoves { get; set; }
    public string? Opening { get; set; }
    public string? Result { get; set; }
}

public class GameImageDto
{
    public Guid ImageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? CloudStorageUrl { get; set; }
    public bool IsStoredInCloud { get; set; }
    public DateTime UploadedAt { get; set; }
}

