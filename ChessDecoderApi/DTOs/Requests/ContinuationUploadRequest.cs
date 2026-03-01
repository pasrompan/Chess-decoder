using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for adding a continuation page to an existing game.
/// </summary>
public class ContinuationUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool AutoCrop { get; set; } = false;
}
