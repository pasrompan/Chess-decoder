using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for updating game PGN content
/// </summary>
public class UpdatePgnRequest
{
    [Required]
    public string PgnContent { get; set; } = string.Empty;
}
