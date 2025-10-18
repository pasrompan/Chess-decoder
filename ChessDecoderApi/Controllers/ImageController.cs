using Microsoft.AspNetCore.Mvc;
using ChessDecoderApi.Services;

namespace ChessDecoderApi.Controllers;

/// <summary>
/// Controller for cloud storage operations (simplified - game processing moved to GameController)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<ImageController> _logger;

    public ImageController(
        ICloudStorageService cloudStorageService,
        ILogger<ImageController> logger)
    {
        _cloudStorageService = cloudStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Download an image from Cloud Storage
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
