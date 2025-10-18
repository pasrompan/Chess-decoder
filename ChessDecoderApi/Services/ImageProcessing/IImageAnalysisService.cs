using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChessDecoderApi.Services.ImageProcessing;

/// <summary>
/// Service for analyzing images to detect tables, columns, and corners
/// </summary>
public interface IImageAnalysisService
{
    /// <summary>
    /// Find the boundaries of the chess table in an image
    /// </summary>
    Rectangle FindTableBoundaries(Image<Rgba32> image);

    /// <summary>
    /// Automatically detect chess columns within an image or search region
    /// </summary>
    /// <param name="image">The image to analyze</param>
    /// <param name="searchRegion">Optional region to restrict search</param>
    /// <param name="useHeuristics">Whether to use heuristics or fallback to equal division</param>
    List<int> DetectChessColumnsAutomatically(Image<Rgba32> image, Rectangle? searchRegion = null, bool useHeuristics = true);

    /// <summary>
    /// Split an image into columns using projection profile analysis
    /// </summary>
    List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns = 6);

    /// <summary>
    /// Split an image into columns within a specific search region
    /// </summary>
    List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns, Rectangle? searchRegion);

    /// <summary>
    /// Get detected corners in an image
    /// </summary>
    List<Point> GetDetectedCorners(Image<Rgba32> image);

    /// <summary>
    /// Get detailed corner information for debugging
    /// </summary>
    Dictionary<string, object> GetDetailedCornerInfo(Image<Rgba32> image);
}

