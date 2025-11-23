namespace ChessDecoderApi.Services.ImageProcessing;

/// <summary>
/// Service for manipulating images (cropping, adding boundaries, etc.)
/// </summary>
public interface IImageManipulationService
{
    /// <summary>
    /// Crop an image to specified dimensions
    /// </summary>
    Task<byte[]> CropImageAsync(string imagePath, int x, int y, int width, int height);

    /// <summary>
    /// Create an image with column boundaries drawn for visualization
    /// </summary>
    Task<byte[]> CreateImageWithBoundariesAsync(string imagePath);
}

