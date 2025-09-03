using Microsoft.AspNetCore.Mvc;
using ChessDecoderApi.Services;
using System.ComponentModel.DataAnnotations;

namespace ChessDecoderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ImageController> _logger;

    public ImageController(
        ICloudStorageService cloudStorageService,
        IImageProcessingService imageProcessingService,
        ILogger<ImageController> logger)
    {
        _cloudStorageService = cloudStorageService;
        _imageProcessingService = imageProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Upload an image to Cloud Storage and process it for chess moves
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage([FromForm] ImageUploadRequest request)
    {
        try
        {
            if (request.Image == null || request.Image.Length == 0)
            {
                return BadRequest("No image file provided");
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(request.Image.ContentType.ToLower()))
            {
                return BadRequest("Invalid file type. Only JPEG, PNG, and WebP images are allowed.");
            }

            // Validate file size (max 10MB)
            if (request.Image.Length > 10 * 1024 * 1024)
            {
                return BadRequest("File size too large. Maximum size is 10MB.");
            }

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(request.Image.FileName)}";
            
            // Upload to Cloud Storage
            using var stream = request.Image.OpenReadStream();
            var objectName = await _cloudStorageService.UploadGameImageAsync(
                stream, 
                fileName, 
                request.Image.ContentType);

            // Get the public URL
            var imageUrl = await _cloudStorageService.GetImageUrlAsync(objectName);

            // Process the image for chess moves
            var language = request.Language ?? "English";
            var result = await _imageProcessingService.ProcessImageAsync(imageUrl, language);

            return Ok(new
            {
                success = true,
                imageUrl = imageUrl,
                objectName = objectName,
                result = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading and processing image");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Process an image from a URL for chess moves
    /// </summary>
    [HttpPost("process-url")]
    public async Task<IActionResult> ProcessImageFromUrl([FromBody] ProcessUrlRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ImageUrl))
            {
                return BadRequest("Image URL is required");
            }

            var language = request.Language ?? "English";
            var result = await _imageProcessingService.ProcessImageAsync(request.ImageUrl, language);

            return Ok(new
            {
                success = true,
                imageUrl = request.ImageUrl,
                result = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing image from URL");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get an image from Cloud Storage
    /// </summary>
    [HttpGet("download/{fileName}")]
    public async Task<IActionResult> DownloadImage(string fileName)
    {
        try
        {
            var stream = await _cloudStorageService.DownloadGameImageAsync(fileName);
            return File(stream, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image: {FileName}", fileName);
            return NotFound(new { success = false, message = "Image not found" });
        }
    }

    /// <summary>
    /// Delete an image from Cloud Storage
    /// </summary>
    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeleteImage(string fileName)
    {
        try
        {
            var success = await _cloudStorageService.DeleteGameImageAsync(fileName);
            
            if (success)
            {
                return Ok(new { success = true, message = "Image deleted successfully" });
            }
            else
            {
                return NotFound(new { success = false, message = "Image not found or could not be deleted" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image: {FileName}", fileName);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}

public class ImageUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;
    
    public string? Language { get; set; } = "English";
}

public class ProcessUrlRequest
{
    [Required]
    public string ImageUrl { get; set; } = null!;
    
    public string? Language { get; set; } = "English";
}
