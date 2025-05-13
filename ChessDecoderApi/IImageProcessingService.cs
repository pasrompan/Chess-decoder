namespace ChessDecoderApi.Services
{
    public interface IImageProcessingService
    {
        Task<string> ProcessImageAsync(string imagePath);
        Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language);
        Task<string> GeneratePGNContentAsync(IEnumerable<string> moves);
        Task<string> DebugUploadAsync(string imagePath, string promptText);
    }
}