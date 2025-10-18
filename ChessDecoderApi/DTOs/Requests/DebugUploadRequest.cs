using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for debug image upload with custom prompt
/// </summary>
public class DebugUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;

    [Required]
    public string PromptText { get; set; } = string.Empty;
}

