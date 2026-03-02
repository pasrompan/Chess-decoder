using ChessDecoderApi.Models;

namespace ChessDecoderApi.DTOs.Responses;

public class GamePageInfoResponse
{
    public Guid ImageId { get; set; }
    public int PageNumber { get; set; }
    public int StartingMoveNumber { get; set; }
    public int EndingMoveNumber { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Variant { get; set; } = "original";
}

public class ContinuationValidationResponse
{
    public bool IsValid { get; set; }
    public int Page1EndMove { get; set; }
    public int Page2StartMove { get; set; }
    public bool HasGap { get; set; }
    public int? GapSize { get; set; }
    public bool HasOverlap { get; set; }
    public int? OverlapMoves { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class DualGameUploadResponse
{
    public Guid GameId { get; set; }
    public string MergedPgn { get; set; } = string.Empty;
    public int TotalMoves { get; set; }
    public ChessGameValidation? Validation { get; set; }
    public double ProcessingTimeMs { get; set; }
    public int CreditsRemaining { get; set; }
    public GamePageInfoResponse Page1 { get; set; } = new();
    public GamePageInfoResponse Page2 { get; set; } = new();
    public ContinuationValidationResponse ContinuationValidation { get; set; } = new();
}

public class ContinuationUploadResponse
{
    public Guid GameId { get; set; }
    public string UpdatedPgn { get; set; } = string.Empty;
    public int TotalMoves { get; set; }
    public ChessGameValidation? Validation { get; set; }
    public double ProcessingTimeMs { get; set; }
    public GamePageInfoResponse Page2 { get; set; } = new();
    public ContinuationValidationResponse ContinuationValidation { get; set; } = new();
}

public class GamePagesResponse
{
    public Guid GameId { get; set; }
    public bool HasContinuation { get; set; }
    public GamePageInfoResponse? Page1 { get; set; }
    public GamePageInfoResponse? Page2 { get; set; }
}

public class DeleteContinuationResponse
{
    public Guid GameId { get; set; }
    public string UpdatedPgn { get; set; } = string.Empty;
    public int TotalMoves { get; set; }
    public bool HasContinuation { get; set; }
}
