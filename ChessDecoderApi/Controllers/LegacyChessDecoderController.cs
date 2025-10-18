using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.Services.GameProcessing;
using ChessDecoderApi.Services.ImageProcessing;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ChessDecoderApi.Controllers;

/// <summary>
/// Legacy controller for backward compatibility with old routes.
/// Maps old ChessDecoder routes to new controller actions.
/// This controller should be removed once all clients migrate to new routes.
/// </summary>
[ApiController]
[Route("[controller]")]
[Obsolete("Use new controllers (GameController, DebugController, etc.) instead. This controller is for backward compatibility only.")]
public class ChessDecoderController : ControllerBase
{
    private readonly IGameProcessingService _gameProcessingService;
    private readonly IImageExtractionService _imageExtractionService;
    private readonly IImageAnalysisService _imageAnalysisService;
    private readonly IImageManipulationService _imageManipulationService;
    private readonly ILogger<ChessDecoderController> _logger;

    public ChessDecoderController(
        IGameProcessingService gameProcessingService,
        IImageExtractionService imageExtractionService,
        IImageAnalysisService imageAnalysisService,
        IImageManipulationService imageManipulationService,
        ILogger<ChessDecoderController> logger)
    {
        _gameProcessingService = gameProcessingService;
        _imageExtractionService = imageExtractionService;
        _imageAnalysisService = imageAnalysisService;
        _imageManipulationService = imageManipulationService;
        _logger = logger;
    }

    /// <summary>
    /// Legacy health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CheckHealth()
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/health called. Please migrate to /api/game/health");
        return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Legacy upload endpoint (v2) - redirects to GameController
    /// </summary>
    [HttpPost("upload/v2")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadImageV2([FromForm] GameUploadRequest request)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/upload/v2 called. Please migrate to /api/game/upload");
        
        try
        {
            var response = await _gameProcessingService.ProcessGameUploadAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during game upload for user {UserId}", request.UserId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing game upload for user {UserId}", request.UserId);
            return StatusCode(500, new { message = "Failed to process image" });
        }
    }

    /// <summary>
    /// Legacy mock upload endpoint - redirects to MockController
    /// </summary>
    [HttpPost("mockupload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MockUpload(
        IFormFile? image, 
        [FromForm] string language = "English",
        [FromForm] bool autoCrop = false)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/mockupload called. Please migrate to /api/mock/upload");

        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        if (!image.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { message = "Uploaded file must be an image" });
        }

        try
        {
            var response = await _gameProcessingService.ProcessMockUploadAsync(image, language, autoCrop);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mock upload with autoCrop: {AutoCrop}", autoCrop);
            return StatusCode(500, new { message = "Internal server error: " + ex.Message });
        }
    }

    /// <summary>
    /// Legacy debug upload endpoint
    /// </summary>
    [HttpPost("debug/upload")]
    public async Task<IActionResult> DebugUpload(IFormFile? image, [FromForm] string promptText)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/debug/upload called. Please migrate to /api/debug/upload");

        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        if (string.IsNullOrWhiteSpace(promptText))
        {
            return BadRequest(new { message = "Prompt text is required" });
        }

        if (!image.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { message = "Uploaded file must be an image" });
        }

        try
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var rawResponse = await _imageExtractionService.DebugUploadAsync(tempFilePath, promptText);
                return Ok(new { response = rawResponse });
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debug image");
            return StatusCode(500, new { message = "Failed to process debug image" });
        }
    }

    /// <summary>
    /// Legacy debug split columns endpoint
    /// </summary>
    [HttpPost("debug/split-columns")]
    public async Task<IActionResult> DebugSplitColumns(
        IFormFile? image,
        [FromForm] int expectedColumns = 6)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/debug/split-columns called. Please migrate to /api/debug/split-columns");

        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        if (!image.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { message = "Uploaded file must be an image" });
        }

        try
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                using var loadedImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(tempFilePath);
                
                var tableBoundaries = _imageAnalysisService.FindTableBoundaries(loadedImage);
                var columnBoundaries = _imageAnalysisService.DetectChessColumnsAutomatically(loadedImage, tableBoundaries);
                
                return Ok(new
                {
                    imageFileName = image.FileName,
                    imageWidth = columnBoundaries.Last(),
                    imageHeight = loadedImage.Height,
                    expectedColumns = expectedColumns,
                    columnBoundaries = columnBoundaries,
                    columnData = columnBoundaries.Take(columnBoundaries.Count - 1).Select((start, index) => new
                    {
                        columnIndex = index,
                        startPosition = start,
                        endPosition = columnBoundaries[index + 1],
                        width = columnBoundaries[index + 1] - start
                    }).ToArray(),
                    tableBoundaries = new
                    {
                        x = tableBoundaries.X,
                        y = tableBoundaries.Y,
                        width = tableBoundaries.Width,
                        height = tableBoundaries.Height
                    }
                });
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debug split columns");
            return StatusCode(500, new { message = "Failed to process image column splitting: " + ex.Message });
        }
    }

    /// <summary>
    /// Legacy debug image with boundaries endpoint
    /// </summary>
    [HttpPost("debug/image-with-boundaries")]
    public async Task<IActionResult> DebugImageWithBoundaries(
        IFormFile? image,
        [FromForm] int expectedColumns = 6)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/debug/image-with-boundaries called. Please migrate to /api/debug/image-with-boundaries");

        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        try
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var imageWithBoundaries = await _imageManipulationService.CreateImageWithBoundariesAsync(tempFilePath, expectedColumns);
                return File(imageWithBoundaries, "image/jpeg", $"boundaries_{image.FileName}");
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debug image with boundaries");
            return StatusCode(500, new { message = "Failed to process image with boundaries: " + ex.Message });
        }
    }

    /// <summary>
    /// Legacy debug table boundaries endpoint
    /// </summary>
    [HttpPost("debug/table-boundaries")]
    public async Task<IActionResult> DebugTableBoundaries(IFormFile? image)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/debug/table-boundaries called. Please migrate to /api/debug/table-boundaries");

        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        try
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                using var loadedImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(tempFilePath);
                var tableBoundaries = _imageAnalysisService.FindTableBoundaries(loadedImage);
                
                using var imageWithTableBoundaries = loadedImage.Clone();
                DrawTableBoundariesOnImage(imageWithTableBoundaries, tableBoundaries);
                
                using var ms = new MemoryStream();
                await imageWithTableBoundaries.SaveAsJpegAsync(ms);
                var imageBytes = ms.ToArray();

                return File(imageBytes, "image/jpeg", $"table_boundaries_{image.FileName}");
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debug table boundaries");
            return StatusCode(500, new { message = "Failed to process table boundaries: " + ex.Message });
        }
    }

    /// <summary>
    /// Legacy debug crop image endpoint
    /// </summary>
    [HttpPost("debug/crop-image")]
    public async Task<IActionResult> DebugCropImage(
        IFormFile? image,
        [FromForm] int x,
        [FromForm] int y,
        [FromForm] int width,
        [FromForm] int height)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/debug/crop-image called. Please migrate to /api/debug/crop-image");

        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        try
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var croppedImageBytes = await _imageManipulationService.CropImageAsync(tempFilePath, x, y, width, height);
                return File(croppedImageBytes, "image/jpeg", $"cropped_{image.FileName}");
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debug crop image");
            return StatusCode(500, new { message = "Failed to crop image: " + ex.Message });
        }
    }

    /// <summary>
    /// Legacy debug table analysis endpoint
    /// </summary>
    [HttpPost("debug/table-analysis")]
    public async Task<IActionResult> DebugTableAnalysis(IFormFile? image)
    {
        _logger.LogWarning("Legacy endpoint /ChessDecoder/debug/table-analysis called. Please migrate to /api/debug/table-analysis");

        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        try
        {
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                using var loadedImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(tempFilePath);
                var tableBoundaries = _imageAnalysisService.FindTableBoundaries(loadedImage);
                
                var imageWidth = loadedImage.Width;
                var imageHeight = loadedImage.Height;
                var tableArea = tableBoundaries.Width * tableBoundaries.Height;
                var imageArea = imageWidth * imageHeight;
                var tablePercentage = (double)tableArea / imageArea * 100;
                
                return Ok(new
                {
                    success = true,
                    imageDimensions = new { width = imageWidth, height = imageHeight },
                    tableBoundaries = new
                    {
                        x = tableBoundaries.X,
                        y = tableBoundaries.Y,
                        width = tableBoundaries.Width,
                        height = tableBoundaries.Height
                    },
                    analysis = new
                    {
                        tableArea = tableArea,
                        imageArea = imageArea,
                        tablePercentage = Math.Round(tablePercentage, 2),
                        tableCenterX = tableBoundaries.X + tableBoundaries.Width / 2,
                        tableCenterY = tableBoundaries.Y + tableBoundaries.Height / 2,
                        imageCenterX = imageWidth / 2,
                        imageCenterY = imageHeight / 2
                    }
                });
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debug table analysis");
            return StatusCode(500, new { message = "Failed to process table analysis: " + ex.Message });
        }
    }

    private void DrawTableBoundariesOnImage(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image, SixLabors.ImageSharp.Rectangle tableBoundaries)
    {
        var blue = new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 255, 255);
        int thickness = 6;
        
        // Draw top boundary
        for (int x = tableBoundaries.X; x < tableBoundaries.X + tableBoundaries.Width; x++)
        {
            for (int offset = 0; offset < thickness; offset++)
            {
                int y = tableBoundaries.Y + offset;
                if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                {
                    image[x, y] = blue;
                }
            }
        }
        
        // Draw bottom boundary
        for (int x = tableBoundaries.X; x < tableBoundaries.X + tableBoundaries.Width; x++)
        {
            for (int offset = 0; offset < thickness; offset++)
            {
                int y = tableBoundaries.Y + tableBoundaries.Height - offset;
                if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                {
                    image[x, y] = blue;
                }
            }
        }
        
        // Draw left boundary
        for (int y = tableBoundaries.Y; y < tableBoundaries.Y + tableBoundaries.Height; y++)
        {
            for (int offset = 0; offset < thickness; offset++)
            {
                int x = tableBoundaries.X + offset;
                if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                {
                    image[x, y] = blue;
                }
            }
        }
        
        // Draw right boundary
        for (int y = tableBoundaries.Y; y < tableBoundaries.Y + tableBoundaries.Height; y++)
        {
            for (int offset = 0; offset < thickness; offset++)
            {
                int x = tableBoundaries.X + tableBoundaries.Width - offset;
                if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                {
                    image[x, y] = blue;
                }
            }
        }
    }
}

