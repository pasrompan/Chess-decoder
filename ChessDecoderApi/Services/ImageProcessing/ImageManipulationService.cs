namespace ChessDecoderApi.Services.ImageProcessing;

/// <summary>
/// Service for manipulating images (cropping, adding boundaries, etc.).
/// Currently wraps ImageProcessingService - can be fully extracted later for complete separation.
/// </summary>
public class ImageManipulationService : IImageManipulationService
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ImageManipulationService> _logger;

    public ImageManipulationService(
        IImageProcessingService imageProcessingService,
        ILogger<ImageManipulationService> logger)
    {
        _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> CropImageAsync(string imagePath, int x, int y, int width, int height)
    {
        return await _imageProcessingService.CropImageAsync(imagePath, x, y, width, height);
    }

    public async Task<byte[]> CreateImageWithBoundariesAsync(string imagePath, int expectedColumns = 6)
    {
        return await _imageProcessingService.CreateImageWithBoundariesAsync(imagePath, expectedColumns);
    }
}

