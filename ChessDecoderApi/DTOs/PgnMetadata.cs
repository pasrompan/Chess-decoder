namespace ChessDecoderApi.DTOs;

/// <summary>
/// Metadata for PGN format generation including player names, date, and round
/// </summary>
public class PgnMetadata
{
    public string? WhitePlayer { get; set; }
    public string? BlackPlayer { get; set; }
    public DateTime? GameDate { get; set; }
    public string? Round { get; set; }
}

