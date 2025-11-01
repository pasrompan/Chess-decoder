using ChessDecoderApi.Services.GameProcessing;
using Microsoft.AspNetCore.Mvc;

namespace ChessDecoderApi.Controllers;

/// <summary>
/// Controller for mock/testing endpoints (no credit deduction, no database saves)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MockController : ControllerBase
{
    private readonly IGameProcessingService _gameProcessingService;
    private readonly ILogger<MockController> _logger;

    public MockController(
        IGameProcessingService gameProcessingService,
        ILogger<MockController> logger)
    {
        _gameProcessingService = gameProcessingService ?? throw new ArgumentNullException(nameof(gameProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Mock image upload for testing (no credits deducted, no database save)
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MockUpload(
        IFormFile? image, 
        [FromForm] string language = "English",
        [FromForm] bool autoCrop = false,
        [FromForm] int expectedColumns = 4)
    {
        _logger.LogInformation("Processing mock upload request with autoCrop: {AutoCrop}, expectedColumns: {ExpectedColumns}", autoCrop, expectedColumns);

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
            var response = await _gameProcessingService.ProcessMockUploadAsync(image, language, autoCrop, expectedColumns);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mock upload with autoCrop: {AutoCrop}, expectedColumns: {ExpectedColumns}", autoCrop, expectedColumns);
            return StatusCode(500, new { message = "Internal server error: " + ex.Message });
        }
    }
}

