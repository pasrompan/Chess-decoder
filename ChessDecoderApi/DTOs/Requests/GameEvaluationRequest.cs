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

    public bool AutoCrop { get; set; } = false;
}

