namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for updating game metadata (player details)
/// </summary>
public class UpdateGameMetadataRequest
{
    public string? WhitePlayer { get; set; }
    public string? BlackPlayer { get; set; }
    public DateTime? GameDate { get; set; }
    public string? Round { get; set; }
}

