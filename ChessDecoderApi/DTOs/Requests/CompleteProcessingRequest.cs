namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for marking processing as complete
/// </summary>
public class CompleteProcessingRequest
{
    public string UserId { get; set; } = string.Empty;
}

