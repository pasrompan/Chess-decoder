using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services.ImageProcessing;

/// <summary>
/// Service for extracting text and chess moves from images.
/// Currently wraps ImageProcessingService - can be fully extracted later for complete separation.
/// </summary>
public class ImageExtractionService : IImageExtractionService
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ImageExtractionService> _logger;

    public ImageExtractionService(
        IImageProcessingService imageProcessingService,
        ILogger<ImageExtractionService> logger)
    {
        _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChessGameResponse> ProcessImageAsync(string imagePath, string language = "English", int expectedColumns = 6, bool useWholeImageProcessing = false)
    {
        return await _imageProcessingService.ProcessImageAsync(imagePath, language, expectedColumns, useWholeImageProcessing);
    }

    public async Task<(List<string> whiteMoves, List<string> blackMoves)> ExtractMovesFromImageToStringAsync(
        string imagePath, 
        string language = "English",
        int expectedColumns = 6)
    {
        return await _imageProcessingService.ExtractMovesFromImageToStringAsync(imagePath, language, expectedColumns);
    }

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language, string provider = "gemini")
    {
        return await _imageProcessingService.ExtractTextFromImageAsync(imageBytes, language, provider);
    }

    public async Task<string> DebugUploadAsync(string imagePath, string promptText)
    {
        return await _imageProcessingService.DebugUploadAsync(imagePath, promptText);
    }

    public string GeneratePGNContent(IEnumerable<string> whiteMoves, IEnumerable<string> blackMoves)
    {
        return _imageProcessingService.GeneratePGNContentAsync(whiteMoves, blackMoves);
    }
}

