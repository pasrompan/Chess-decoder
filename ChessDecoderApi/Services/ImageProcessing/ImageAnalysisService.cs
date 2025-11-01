using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChessDecoderApi.Services.ImageProcessing;

/// <summary>
/// Service for analyzing images to detect tables, columns, and corners.
/// Currently wraps ImageProcessingService - can be fully extracted later for complete separation.
/// </summary>
public class ImageAnalysisService : IImageAnalysisService
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ImageAnalysisService> _logger;

    public ImageAnalysisService(
        IImageProcessingService imageProcessingService,
        ILogger<ImageAnalysisService> logger)
    {
        _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Rectangle FindTableBoundaries(Image<Rgba32> image)
    {
        return _imageProcessingService.FindTableBoundaries(image);
    }

    public List<int> DetectChessColumnsAutomatically(Image<Rgba32> image, Rectangle? searchRegion = null, bool useHeuristics = true, int expectedColumns = 4)
    {
        return _imageProcessingService.DetectChessColumnsAutomatically(image, searchRegion, useHeuristics, expectedColumns);
    }

    public List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns = 6)
    {
        return _imageProcessingService.SplitImageIntoColumns(image, expectedColumns);
    }

    public List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns, Rectangle? searchRegion)
    {
        return _imageProcessingService.SplitImageIntoColumns(image, expectedColumns, searchRegion);
    }

    public List<Point> GetDetectedCorners(Image<Rgba32> image)
    {
        return _imageProcessingService.GetDetectedCorners(image);
    }

    public Dictionary<string, object> GetDetailedCornerInfo(Image<Rgba32> image)
    {
        return _imageProcessingService.GetDetailedCornerInfo(image);
    }
}

