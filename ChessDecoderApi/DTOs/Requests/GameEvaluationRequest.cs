using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for evaluating an image against ground truth
/// </summary>
public class GameEvaluationRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;

    [Required]
    public IFormFile GroundTruth { get; set; } = null!;

    public string Language { get; set; } = "English";

    public int NumberOfColumns { get; set; } = 4;

    public bool AutoCrop { get; set; } = false;

    /// <summary>
    /// If true, uses whole image processing (sends entire image to LLM without column splitting).
    /// If false, uses column splitting approach (default).
    /// </summary>
    public bool UseWholeImageProcessing { get; set; } = false;
}

