using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services
{
    public interface IImageProcessingService
    {
        Task<ChessGameResponse> ProcessImageAsync(string imagePath, string language = "English");
        Task<string[]> ExtractMovesFromImageToStringAsync(string imagePath, string language = "English");
        Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language);
        Task<string> GeneratePGNContentAsync(IEnumerable<string> moves);
        Task<string> DebugUploadAsync(string imagePath, string promptText);
    }
} 