namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for updating PGN content
/// </summary>
public class UpdatePgnRequest
{
    public string PgnContent { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

