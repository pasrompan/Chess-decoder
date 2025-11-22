using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Services;
using ChessDecoderApi.Services.ImageProcessing;
using Microsoft.AspNetCore.Mvc;

namespace ChessDecoderApi.Controllers;

/// <summary>
/// Controller for image evaluation and testing endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EvaluationController : ControllerBase
{
    private readonly IImageExtractionService _imageExtractionService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EvaluationController> _logger;

    public EvaluationController(
        IImageExtractionService imageExtractionService,
        ILoggerFactory loggerFactory,
        ILogger<EvaluationController> logger)
    {
        _imageExtractionService = imageExtractionService ?? throw new ArgumentNullException(nameof(imageExtractionService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Evaluate an image against ground truth data
    /// </summary>
    [HttpPost("evaluate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EvaluateImage([FromForm] GameEvaluationRequest request)
    {
        _logger.LogInformation("Processing image evaluation request");

        if (request.Image == null || request.Image.Length == 0)
        {
            return BadRequest(new { message = "No image file provided for evaluation" });
        }

        if (request.GroundTruth == null || request.GroundTruth.Length == 0)
        {
            return BadRequest(new { message = "No ground truth file provided for evaluation" });
        }

        if (!request.Image.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { message = "Image file must be an image" });
        }

        if (!request.GroundTruth.ContentType.StartsWith("text/") && 
            !request.GroundTruth.FileName.EndsWith(".txt") && 
            !request.GroundTruth.FileName.EndsWith(".pgn"))
        {
            return BadRequest(new { message = "Ground truth file must be a text file (.txt or .pgn)" });
        }

        try
        {
            var tempImagePath = Path.GetTempFileName();
            var tempGroundTruthPath = Path.GetTempFileName();
            
            try
            {
                using (var stream = new FileStream(tempImagePath, FileMode.Create))
                {
                    await request.Image.CopyToAsync(stream);
                }

                using (var stream = new FileStream(tempGroundTruthPath, FileMode.Create))
                {
                    await request.GroundTruth.CopyToAsync(stream);
                }

                // Create evaluation service - this uses the legacy ImageProcessingService
                // In a full refactor, this would be extracted to its own service
                var imageProcessingService = HttpContext.RequestServices.GetRequiredService<IImageProcessingService>();
                var evaluationLogger = _loggerFactory.CreateLogger<ImageProcessingEvaluationService>();
                var evaluationService = new ImageProcessingEvaluationService(
                    imageProcessingService, 
                    evaluationLogger, 
                    useRealApi: true);

                var result = await evaluationService.EvaluateAsync(tempImagePath, tempGroundTruthPath, request.Language, request.NumberOfColumns, request.AutoCrop);

                return Ok(new EvaluationResultResponse
                {
                    ImageFileName = request.Image.FileName,
                    GroundTruthFileName = request.GroundTruth.FileName,
                    Language = request.Language,
                    IsSuccessful = result.IsSuccessful,
                    ErrorMessage = result.ErrorMessage,
                    ProcessingTimeSeconds = result.ProcessingTime.TotalSeconds,
                    Metrics = new EvaluationMetricsDto
                    {
                        NormalizedScore = Math.Round(result.NormalizedScore, 3),
                        ExactMatchScore = Math.Round(result.ExactMatchScore, 3),
                        PositionalAccuracy = Math.Round(result.PositionalAccuracy, 3),
                        LevenshteinDistance = result.LevenshteinDistance,
                        LongestCommonSubsequence = result.LongestCommonSubsequence
                    },
                    MoveCounts = new MoveCountsDto
                    {
                        GroundTruthMoves = result.GroundTruthMoves.Count,
                        ExtractedMoves = result.ExtractedMoves.Count
                    },
                    Moves = new MovesDto
                    {
                        GroundTruth = result.GroundTruthMoves,
                        Extracted = result.ExtractedMoves
                    },
                    GeneratedPgn = result.GeneratedPgn
                });
            }
            finally
            {
                if (System.IO.File.Exists(tempImagePath))
                {
                    System.IO.File.Delete(tempImagePath);
                }
                if (System.IO.File.Exists(tempGroundTruthPath))
                {
                    System.IO.File.Delete(tempGroundTruthPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during image evaluation");
            return StatusCode(500, new { message = "Failed to evaluate image: " + ex.Message });
        }
    }
}

