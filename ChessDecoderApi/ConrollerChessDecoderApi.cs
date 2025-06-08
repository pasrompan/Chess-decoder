using ChessDecoderApi.Models;
using ChessDecoderApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ChessDecoderApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChessDecoderController : ControllerBase
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly ILogger<ChessDecoderController> _logger;

        public ChessDecoderController(
            IImageProcessingService imageProcessingService,
            ILogger<ChessDecoderController> logger)
        {
            _imageProcessingService = imageProcessingService;
            _logger = logger;
        }

        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult CheckHealth()
        {
            return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
        }

        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadImage(IFormFile? image, [FromForm] string language = "English")
        {
            _logger.LogInformation("Processing image upload request");

            if (image == null || image.Length == 0)
            {
                _logger.LogWarning("No image file provided");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "No image file provided" 
                });
            }

            // Validate file is an image
            if (!image.ContentType.StartsWith("image/"))
            {
                _logger.LogWarning("Uploaded file is not an image");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Uploaded file must be an image" 
                });
            }

            try
            {
                // Save to temp file
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var stream = new FileStream(tempFilePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    // Process the image
                    var result = await _imageProcessingService.ProcessImageAsync(tempFilePath, language);

                    // Return both PGN content and validation data
                    return Ok(new
                    {
                        pgnContent = result.PgnContent,
                        validation = result.Validation
                    });
                }
                finally
                {
                    // Clean up temp file
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("Unauthorized access to OpenAI API");
                return StatusCode(StatusCodes.Status401Unauthorized, new ErrorResponse 
                { 
                    Status = StatusCodes.Status401Unauthorized, 
                    Message = "Unauthorized access to image processing service" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to process image" 
                });
            }
        }

        [HttpPost("debug/upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DebugUpload(IFormFile? image, [FromForm] string promptText)
        {
            _logger.LogInformation("Processing debug image upload request");

            if (image == null || image.Length == 0)
            {
                _logger.LogWarning("No image file provided");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "No image file provided" 
                });
            }

            if (string.IsNullOrWhiteSpace(promptText))
            {
                _logger.LogWarning("No prompt text provided");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Prompt text is required" 
                });
            }

            // Validate file is an image
            if (!image.ContentType.StartsWith("image/"))
            {
                _logger.LogWarning("Uploaded file is not an image");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Uploaded file must be an image" 
                });
            }

            try
            {
                // Save to temp file
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var stream = new FileStream(tempFilePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    // Process the image with debug endpoint
                    var rawResponse = await _imageProcessingService.DebugUploadAsync(tempFilePath, promptText);

                    // Return the raw response
                    return Ok(new { response = rawResponse });
                }
                finally
                {
                    // Clean up temp file
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("Unauthorized access to OpenAI API");
                return StatusCode(StatusCodes.Status401Unauthorized, new ErrorResponse 
                { 
                    Status = StatusCodes.Status401Unauthorized, 
                    Message = "Unauthorized access to image processing service" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing debug image");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to process debug image" 
                });
            }
        }
    }
}