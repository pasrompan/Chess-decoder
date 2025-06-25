using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services
{
    public interface IImageProcessingService
    {
        Task<ChessGameResponse> ProcessImageAsync(string imagePath, string language = "English");
        Task<(List<string> whiteMoves, List<string> blackMoves)> ExtractMovesFromImageToStringAsync(string imagePath, string language = "English");
        Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language);
        Task<string> GeneratePGNContentAsync(IEnumerable<string> whiteMoves, IEnumerable<string> blackMoves);
        Task<string> DebugUploadAsync(string imagePath, string promptText);
    }
} 