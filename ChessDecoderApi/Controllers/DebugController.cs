using ChessDecoderApi.Services.ImageProcessing;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChessDecoderApi.Controllers;

/// <summary>
/// Controller for debug and diagnostic endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IImageExtractionService _imageExtractionService;
    private readonly IImageAnalysisService _imageAnalysisService;
    private readonly IImageManipulationService _imageManipulationService;
    private readonly ILogger<DebugController> _logger;

    public DebugController(
        IImageExtractionService imageExtractionService,
        IImageAnalysisService imageAnalysisService,
        IImageManipulationService imageManipulationService,
        ILogger<DebugController> logger)
    {
        _imageExtractionService = imageExtractionService ?? throw new ArgumentNullException(nameof(imageExtractionService));
        _imageAnalysisService = imageAnalysisService ?? throw new ArgumentNullException(nameof(imageAnalysisService));
        _imageManipulationService = imageManipulationService ?? throw new ArgumentNullException(nameof(imageManipulationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Debug upload with custom prompt
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> DebugUpload(IFormFile? image, [FromForm] string promptText)
    {
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
    /// Debug endpoint to split columns and return boundary information
    /// </summary>
    [HttpPost("split-columns")]
    public async Task<IActionResult> DebugSplitColumns(IFormFile? image)
    {
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

                using var loadedImage = Image.Load<Rgba32>(tempFilePath);
                
                var tableBoundaries = _imageAnalysisService.FindTableBoundaries(loadedImage);
                // Use default 6 columns for auto-detection
                var columnBoundaries = _imageAnalysisService.DetectChessColumnsAutomatically(loadedImage, tableBoundaries, useHeuristics: true, expectedColumns: 6);
                
                var columnWidths = new List<int>();
                for (int i = 0; i < columnBoundaries.Count - 1; i++)
                {
                    columnWidths.Add(columnBoundaries[i + 1] - columnBoundaries[i]);
                }

                return Ok(new
                {
                    imageFileName = image.FileName,
                    imageWidth = columnBoundaries.Last(),
                    imageHeight = loadedImage.Height,
                    actualColumns = columnBoundaries.Count - 1,
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
    /// Debug endpoint to visualize column boundaries on an image
    /// </summary>
    [HttpPost("image-with-boundaries")]
    public async Task<IActionResult> DebugImageWithBoundaries(IFormFile? image)
    {
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

                var imageWithBoundaries = await _imageManipulationService.CreateImageWithBoundariesAsync(tempFilePath);
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
    /// Debug endpoint to visualize table boundaries
    /// </summary>
    [HttpPost("table-boundaries")]
    public async Task<IActionResult> DebugTableBoundaries(IFormFile? image)
    {
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

                using var loadedImage = Image.Load<Rgba32>(tempFilePath);
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
    /// Debug endpoint to crop an image
    /// </summary>
    [HttpPost("crop-image")]
    public async Task<IActionResult> DebugCropImage(
        IFormFile? image,
        [FromForm] int x,
        [FromForm] int y,
        [FromForm] int width,
        [FromForm] int height)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided" });
        }

        if (!image.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { message = "Uploaded file must be an image" });
        }

        if (width <= 0 || height <= 0)
        {
            return BadRequest(new { message = "Width and height must be positive integers" });
        }

        if (x < 0 || y < 0)
        {
            return BadRequest(new { message = "X and Y coordinates cannot be negative" });
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
    /// Debug endpoint to analyze table properties
    /// </summary>
    [HttpPost("table-analysis")]
    public async Task<IActionResult> DebugTableAnalysis(IFormFile? image)
    {
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

                using var loadedImage = Image.Load<Rgba32>(tempFilePath);
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
                    },
                    debugInfo = new
                    {
                        tableXPercentage = Math.Round((double)tableBoundaries.X / imageWidth * 100, 2),
                        tableYPercentage = Math.Round((double)tableBoundaries.Y / imageHeight * 100, 2),
                        tableWidthPercentage = Math.Round((double)tableBoundaries.Width / imageWidth * 100, 2),
                        tableHeightPercentage = Math.Round((double)tableBoundaries.Height / imageHeight * 100, 2)
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

    private void DrawTableBoundariesOnImage(Image<Rgba32> image, Rectangle tableBoundaries)
    {
        var blue = new Rgba32(0, 0, 255, 255);
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

