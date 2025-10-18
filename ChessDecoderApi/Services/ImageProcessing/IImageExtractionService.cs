using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services.ImageProcessing;

/// <summary>
/// Service for extracting text and chess moves from images
/// </summary>
public interface IImageExtractionService
{
    /// <summary>
    /// Process an image and extract chess game data
    /// </summary>
    Task<ChessGameResponse> ProcessImageAsync(string imagePath, string language = "English", bool useColumnDetection = true);

    /// <summary>
    /// Extract chess moves from an image and return separate white and black move lists
    /// </summary>
    Task<(List<string> whiteMoves, List<string> blackMoves)> ExtractMovesFromImageToStringAsync(
        string imagePath, 
        string language = "English", 
        bool useColumnDetection = true);

    /// <summary>
    /// Extract raw text from image bytes using OCR
    /// </summary>
    Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language);

    /// <summary>
    /// Debug endpoint to upload image with custom prompt
    /// </summary>
    Task<string> DebugUploadAsync(string imagePath, string promptText);

    /// <summary>
    /// Generate PGN content from white and black moves
    /// </summary>
    string GeneratePGNContent(IEnumerable<string> whiteMoves, IEnumerable<string> blackMoves);
}

