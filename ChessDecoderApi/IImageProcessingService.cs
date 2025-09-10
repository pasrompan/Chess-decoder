using ChessDecoderApi.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChessDecoderApi.Services
{
    public interface IImageProcessingService
    {
        Task<ChessGameResponse> ProcessImageAsync(string imagePath, string language = "English");
        Task<(List<string> whiteMoves, List<string> blackMoves)> ExtractMovesFromImageToStringAsync(string imagePath, string language = "English");
        Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language);
        string GeneratePGNContentAsync(IEnumerable<string> whiteMoves, IEnumerable<string> blackMoves);
        Task<string> DebugUploadAsync(string imagePath, string promptText);
        List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns = 6);
        List<int> SplitImageIntoColumns(string imagePath, int expectedColumns = 6);
        List<int> SplitImageIntoRows(Image<Rgba32> image, int expectedRows = 0);
        List<int> SplitImageIntoRows(string imagePath, int expectedRows = 0);
        Task<byte[]> CreateImageWithBoundariesAsync(string imagePath, int expectedColumns = 6, int expectedRows = 0);
        Rectangle FindTableBoundaries(Image<Rgba32> image);
        List<Point> GetDetectedCorners(Image<Rgba32> image);
        Dictionary<string, object> GetDetailedCornerInfo(Image<Rgba32> image);
    }
} 