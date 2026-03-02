using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for evaluating dual-page images against ground truth
/// </summary>
public class DualGameEvaluationRequest
{
    [Required]
    public IFormFile Page1 { get; set; } = null!;

    [Required]
    public IFormFile Page2 { get; set; } = null!;

    [Required]
    public IFormFile GroundTruth { get; set; } = null!;

    public string Language { get; set; } = "English";

    public bool AutoCrop { get; set; } = false;
}
