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
                var chessMoveValidator = HttpContext.RequestServices.GetRequiredService<IChessMoveValidator>();
                var evaluationLogger = _loggerFactory.CreateLogger<ImageProcessingEvaluationService>();
                var evaluationService = new ImageProcessingEvaluationService(
                    imageProcessingService, 
                    chessMoveValidator,
                    evaluationLogger, 
                    useRealApi: true);

                var result = await evaluationService.EvaluateAsync(tempImagePath, tempGroundTruthPath, request.Language, request.AutoCrop);

                return Ok(new EvaluationResultResponse
                {
                    ImageFileName = request.Image.FileName,
                    GroundTruthFileName = request.GroundTruth.FileName,
                    Language = request.Language,
                    DetectedLanguage = result.DetectedLanguage,
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
                    NormalizedMetrics = new EvaluationMetricsDto
                    {
                        NormalizedScore = Math.Round(result.NormalizedNormalizedScore, 3),
                        ExactMatchScore = Math.Round(result.NormalizedExactMatchScore, 3),
                        PositionalAccuracy = Math.Round(result.NormalizedPositionalAccuracy, 3),
                        LevenshteinDistance = result.NormalizedLevenshteinDistance,
                        LongestCommonSubsequence = result.NormalizedLongestCommonSubsequence
                    },
                    MoveCounts = new MoveCountsDto
                    {
                        GroundTruthMoves = result.GroundTruthMoves.Count,
                        ExtractedMoves = result.ExtractedMoves.Count,
                        NormalizedMoves = result.NormalizedMoves.Count
                    },
                    Moves = new MovesDto
                    {
                        GroundTruth = result.GroundTruthMoves,
                        Extracted = result.ExtractedMoves,
                        Normalized = result.NormalizedMoves
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

    /// <summary>
    /// Evaluate dual-page images against ground truth data
    /// </summary>
    [HttpPost("evaluate-dual")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EvaluateDualImage([FromForm] DualGameEvaluationRequest request)
    {
        _logger.LogInformation("Processing dual image evaluation request");

        if (request.Page1 == null || request.Page1.Length == 0)
        {
            return BadRequest(new { message = "No page 1 image file provided for evaluation" });
        }

        if (request.Page2 == null || request.Page2.Length == 0)
        {
            return BadRequest(new { message = "No page 2 image file provided for evaluation" });
        }

        if (request.GroundTruth == null || request.GroundTruth.Length == 0)
        {
            return BadRequest(new { message = "No ground truth file provided for evaluation" });
        }

        if (!request.Page1.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { message = "Page 1 file must be an image" });
        }

        if (!request.Page2.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { message = "Page 2 file must be an image" });
        }

        if (!request.GroundTruth.ContentType.StartsWith("text/") && 
            !request.GroundTruth.FileName.EndsWith(".txt") && 
            !request.GroundTruth.FileName.EndsWith(".pgn"))
        {
            return BadRequest(new { message = "Ground truth file must be a text file (.txt or .pgn)" });
        }

        try
        {
            var tempPage1Path = Path.GetTempFileName();
            var tempPage2Path = Path.GetTempFileName();
            var tempGroundTruthPath = Path.GetTempFileName();
            
            try
            {
                using (var stream = new FileStream(tempPage1Path, FileMode.Create))
                {
                    await request.Page1.CopyToAsync(stream);
                }

                using (var stream = new FileStream(tempPage2Path, FileMode.Create))
                {
                    await request.Page2.CopyToAsync(stream);
                }

                using (var stream = new FileStream(tempGroundTruthPath, FileMode.Create))
                {
                    await request.GroundTruth.CopyToAsync(stream);
                }

                var imageProcessingService = HttpContext.RequestServices.GetRequiredService<IImageProcessingService>();
                var chessMoveValidator = HttpContext.RequestServices.GetRequiredService<IChessMoveValidator>();
                var evaluationLogger = _loggerFactory.CreateLogger<ImageProcessingEvaluationService>();
                var evaluationService = new ImageProcessingEvaluationService(
                    imageProcessingService, 
                    chessMoveValidator,
                    evaluationLogger, 
                    useRealApi: true);

                var result = await evaluationService.EvaluateDualAsync(
                    tempPage1Path, 
                    tempPage2Path, 
                    tempGroundTruthPath, 
                    request.Language, 
                    request.AutoCrop);

                return Ok(new EvaluationResultResponse
                {
                    ImageFileName = $"{request.Page1.FileName} + {request.Page2.FileName}",
                    GroundTruthFileName = request.GroundTruth.FileName,
                    Language = request.Language,
                    DetectedLanguage = result.DetectedLanguage,
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
                    NormalizedMetrics = new EvaluationMetricsDto
                    {
                        NormalizedScore = Math.Round(result.NormalizedNormalizedScore, 3),
                        ExactMatchScore = Math.Round(result.NormalizedExactMatchScore, 3),
                        PositionalAccuracy = Math.Round(result.NormalizedPositionalAccuracy, 3),
                        LevenshteinDistance = result.NormalizedLevenshteinDistance,
                        LongestCommonSubsequence = result.NormalizedLongestCommonSubsequence
                    },
                    MoveCounts = new MoveCountsDto
                    {
                        GroundTruthMoves = result.GroundTruthMoves.Count,
                        ExtractedMoves = result.ExtractedMoves.Count,
                        NormalizedMoves = result.NormalizedMoves.Count
                    },
                    Moves = new MovesDto
                    {
                        GroundTruth = result.GroundTruthMoves,
                        Extracted = result.ExtractedMoves,
                        Normalized = result.NormalizedMoves
                    },
                    GeneratedPgn = result.GeneratedPgn
                });
            }
            finally
            {
                if (System.IO.File.Exists(tempPage1Path))
                {
                    System.IO.File.Delete(tempPage1Path);
                }
                if (System.IO.File.Exists(tempPage2Path))
                {
                    System.IO.File.Delete(tempPage2Path);
                }
                if (System.IO.File.Exists(tempGroundTruthPath))
                {
                    System.IO.File.Delete(tempGroundTruthPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dual image evaluation");
            return StatusCode(500, new { message = "Failed to evaluate dual images: " + ex.Message });
        }
    }
}

