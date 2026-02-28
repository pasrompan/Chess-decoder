using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for uploading two scoresheet pages at once.
/// </summary>
public class DualGameUploadRequest
{
    [Required]
    public IFormFile Page1 { get; set; } = null!;

    [Required]
    public IFormFile Page2 { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool AutoCrop { get; set; } = false;

    public string? WhitePlayer { get; set; }
    public string? BlackPlayer { get; set; }
    public DateTime? GameDate { get; set; }
    public string? Round { get; set; }
}
