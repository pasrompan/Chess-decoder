namespace ChessDecoderApi.DTOs.Responses;

/// <summary>
/// Response model for evaluation results
/// </summary>
public class EvaluationResultResponse
{
    public string ImageFileName { get; set; } = string.Empty;
    public string GroundTruthFileName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string DetectedLanguage { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public double ProcessingTimeSeconds { get; set; }
    public EvaluationMetricsDto Metrics { get; set; } = new();
    public EvaluationMetricsDto? NormalizedMetrics { get; set; }
    public MoveCountsDto MoveCounts { get; set; } = new();
    public MovesDto Moves { get; set; } = new();
    public string? GeneratedPgn { get; set; }
}

public class EvaluationMetricsDto
{
    public double NormalizedScore { get; set; }
    public double ExactMatchScore { get; set; }
    public double PositionalAccuracy { get; set; }
    public int LevenshteinDistance { get; set; }
    public int LongestCommonSubsequence { get; set; }
}

public class MoveCountsDto
{
    public int GroundTruthMoves { get; set; }
    public int ExtractedMoves { get; set; }
    public int NormalizedMoves { get; set; }
}

public class MovesDto
{
    public List<string> GroundTruth { get; set; } = new();
    public List<string> Extracted { get; set; } = new();
    public List<string> Normalized { get; set; } = new();
}

