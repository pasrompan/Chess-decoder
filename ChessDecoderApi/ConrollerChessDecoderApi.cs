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

        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadImage(IFormFile? image)
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
                    var pgnContent = await _imageProcessingService.ProcessImageAsync(tempFilePath);

                    // Generate output filename based on input filename
                    var outputFilename = Path.GetFileNameWithoutExtension(image.FileName) + ".pgn";

                    // Return the PGN file
                    return File(Encoding.UTF8.GetBytes(pgnContent), "application/octet-stream", outputFilename);
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
    }
}