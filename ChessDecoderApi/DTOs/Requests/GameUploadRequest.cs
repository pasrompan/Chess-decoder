using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for uploading and processing a chess game image
/// </summary>
public class GameUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public string Language { get; set; } = "English";

    public bool AutoCrop { get; set; } = false;

    public int ExpectedColumns { get; set; } = 4;

    /// <summary>
    /// If true, uses whole image processing (sends entire image to LLM without column splitting).
    /// If false, uses column splitting approach (default).
    /// </summary>
    public bool UseWholeImageProcessing { get; set; } = false;
}

