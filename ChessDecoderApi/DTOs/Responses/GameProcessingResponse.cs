using ChessDecoderApi.Models;

namespace ChessDecoderApi.DTOs.Responses;

/// <summary>
/// Response model for game processing results
/// </summary>
public class GameProcessingResponse
{
    public Guid GameId { get; set; }
    public string? PgnContent { get; set; }
    public ChessGameValidation? Validation { get; set; }
    public double ProcessingTimeMs { get; set; }
    public int CreditsRemaining { get; set; }
    public string? ProcessedImageUrl { get; set; }
}

