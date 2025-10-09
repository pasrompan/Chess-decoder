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
        List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns, Rectangle? searchRegion);
        List<int> SplitImageIntoColumns(string imagePath, int expectedColumns = 6);
        List<int> DetectChessColumnsAutomatically(Image<Rgba32> image, Rectangle? searchRegion = null);
        Task<byte[]> CreateImageWithBoundariesAsync(string imagePath, int expectedColumns = 6);
        Task<byte[]> CropImageAsync(string imagePath, int x, int y, int width, int height);
        Rectangle FindTableBoundaries(Image<Rgba32> image);
        List<Point> GetDetectedCorners(Image<Rgba32> image);
        Dictionary<string, object> GetDetailedCornerInfo(Image<Rgba32> image);
    }
} 