using ChessDecoderApi.Models;
using ChessDecoderApi.Services;
using ChessDecoderApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ChessDecoderApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChessDecoderController : ControllerBase
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly ILogger<ChessDecoderController> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public ChessDecoderController(
            IImageProcessingService imageProcessingService,
            ILogger<ChessDecoderController> logger,
            ILoggerFactory loggerFactory)
        {
            _imageProcessingService = imageProcessingService;
            _logger = logger;
            _loggerFactory = loggerFactory;
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

                    var outputFilename = Path.GetFileNameWithoutExtension(image.FileName) + ".pgn";
                    var pgnContent = result.PgnContent;

                    // Return the PGN file
                    return File(Encoding.UTF8.GetBytes(pgnContent ?? string.Empty), "application/octet-stream", outputFilename);
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

        [HttpPost("upload/v2")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadImageV2(
            IFormFile? image, 
            [FromForm] string language = "English",
            [FromForm] string userId = "")
        {
            _logger.LogInformation("Processing image upload request (v2) for user: {UserId}", userId);

            if (image == null || image.Length == 0)
            {
                _logger.LogWarning("No image file provided");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "No image file provided" 
                });
            }

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is required for image processing");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "User ID is required" 
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
                // Check if user has enough credits
                var creditService = HttpContext.RequestServices.GetRequiredService<ICreditService>();
                if (!await creditService.HasEnoughCreditsAsync(userId, 1))
                {
                    _logger.LogWarning("User {UserId} has insufficient credits for image processing", userId);
                    return BadRequest(new ErrorResponse 
                    { 
                        Status = StatusCodes.Status400BadRequest, 
                        Message = "Insufficient credits. Please purchase more credits to process images." 
                    });
                }

                // Get Cloud Storage service
                var cloudStorageService = HttpContext.RequestServices.GetRequiredService<ICloudStorageService>();
                
                // Generate unique filename for the uploaded image
                var fileName = $"{Guid.NewGuid()}_{image.FileName}";
                string filePath = string.Empty;
                string? cloudStorageUrl = null;
                string? cloudStorageObjectName = null;
                bool isStoredInCloud = false;

                // Try to upload to Cloud Storage first
                try
                {
                    using var imageStream = new MemoryStream();
                    await image.CopyToAsync(imageStream);
                    imageStream.Position = 0;
                    
                    cloudStorageObjectName = await cloudStorageService.UploadGameImageAsync(
                        imageStream, 
                        fileName, 
                        image.ContentType);
                    
                    cloudStorageUrl = await cloudStorageService.GetImageUrlAsync(cloudStorageObjectName);
                    isStoredInCloud = true;
                    
                    _logger.LogInformation("Image uploaded to Cloud Storage: {CloudStorageUrl}", cloudStorageUrl);
                    
                    // Test if the image is accessible for processing
                    try
                    {
                        using var testClient = new HttpClient();
                        var testResponse = await testClient.GetAsync(cloudStorageUrl);
                        if (!testResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Cloud Storage image not accessible for processing (Status: {StatusCode}), falling back to local storage", testResponse.StatusCode);
                            throw new Exception("Cloud Storage image not accessible");
                        }
                    }
                    catch (Exception testEx)
                    {
                        _logger.LogWarning(testEx, "Cloud Storage image accessibility test failed, falling back to local storage");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upload to Cloud Storage or image not accessible, falling back to local storage");
                    
                    // Reset Cloud Storage variables
                    cloudStorageObjectName = null;
                    cloudStorageUrl = null;
                    isStoredInCloud = false;
                    
                    // Fallback to local storage
                    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }
                    
                    filePath = Path.Combine(uploadsDir, fileName);
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }
                    
                    _logger.LogInformation("Image saved locally to: {FilePath}", filePath);
                }

                // Process the image - use Cloud Storage URL if available, otherwise local path
                var imagePathForProcessing = isStoredInCloud ? cloudStorageUrl! : filePath;
                var startTime = DateTime.UtcNow;
                var result = await _imageProcessingService.ProcessImageAsync(imagePathForProcessing, language);
                var processingTime = DateTime.UtcNow - startTime;

                // Get database context
                var dbContext = HttpContext.RequestServices.GetRequiredService<ChessDecoderDbContext>();

                // Create ChessGame record
                var chessGame = new ChessGame
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = $"Chess Game - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                    Description = $"Processed chess game from image upload. Language: {language}",
                    PgnContent = result.PgnContent ?? "",
                    ProcessedAt = DateTime.UtcNow,
                    ProcessingTimeMs = (int)processingTime.TotalMilliseconds,
                    IsValid = result.Validation?.Moves?.All(m => 
                        (m.WhiteMove?.ValidationStatus == "valid" || m.WhiteMove?.ValidationStatus == "warning") &&
                        (m.BlackMove?.ValidationStatus == "valid" || m.BlackMove?.ValidationStatus == "warning")) ?? false
                };

                dbContext.ChessGames.Add(chessGame);

                // Create GameImage record
                var gameImage = new GameImage
                {
                    Id = Guid.NewGuid(),
                    ChessGameId = chessGame.Id,
                    FileName = fileName,
                    FilePath = filePath,
                    FileType = image.ContentType,
                    FileSizeBytes = image.Length,
                    CloudStorageUrl = cloudStorageUrl,
                    CloudStorageObjectName = cloudStorageObjectName,
                    IsStoredInCloud = isStoredInCloud,
                    UploadedAt = DateTime.UtcNow
                };

                dbContext.GameImages.Add(gameImage);

                // Create GameStatistics record
                var totalMoves = result.Validation?.Moves?.Count ?? 0;
                var validMoves = result.Validation?.Moves?.Count(m => 
                    (m.WhiteMove?.ValidationStatus == "valid" || m.WhiteMove?.ValidationStatus == "warning") &&
                    (m.BlackMove?.ValidationStatus == "valid" || m.BlackMove?.ValidationStatus == "warning")) ?? 0;
                var invalidMoves = totalMoves - validMoves;

                var gameStats = new GameStatistics
                {
                    Id = Guid.NewGuid(),
                    ChessGameId = chessGame.Id,
                    TotalMoves = totalMoves,
                    ValidMoves = validMoves,
                    InvalidMoves = invalidMoves,
                    Opening = ExtractOpening(result.PgnContent ?? ""),
                    Result = "In Progress" // Could be enhanced to detect game end
                };

                dbContext.GameStatistics.Add(gameStats);

                // Deduct credits before saving to ensure it's part of the same transaction
                var user = await dbContext.Users.FindAsync(userId);
                if (user != null)
                {
                    user.Credits -= 1;
                    _logger.LogInformation("Deducting 1 credit from user {UserId}. New balance will be: {NewBalance}", 
                        userId, user.Credits);
                }

                // Save all changes to database (including credit deduction)
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully processed image for user {UserId}. Game ID: {GameId}", 
                    userId, chessGame.Id);

                // Return both PGN content and validation data, plus game info
                return Ok(new
                {
                    gameId = chessGame.Id,
                    pgnContent = result.PgnContent,
                    validation = result.Validation,
                    processingTime = processingTime.TotalMilliseconds,
                    creditsRemaining = await creditService.GetUserCreditsAsync(userId)
                });
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
                _logger.LogError(ex, "Error processing image for user {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to process image" 
                });
            }
        }

        private string ExtractOpening(string pgnContent)
        {
            // Simple opening detection based on first few moves
            var moves = pgnContent.Split('\n')
                .Where(line => !line.StartsWith('[') && !string.IsNullOrWhiteSpace(line))
                .SelectMany(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(move => !string.IsNullOrWhiteSpace(move) && !move.Contains('.'))
                .Take(6) // First 3 moves (6 half-moves)
                .ToArray();

            if (moves.Length >= 2)
            {
                var firstMoves = string.Join(" ", moves.Take(2));
                return firstMoves switch
                {
                    "e4 e5" => "Open Game",
                    "d4 d5" => "Closed Game",
                    "e4 c5" => "Sicilian Defense",
                    "d4 Nf6" => "Indian Defense",
                    "e4 e6" => "French Defense",
                    "e4 d5" => "Scandinavian Defense",
                    _ => "Custom Opening"
                };
            }

            return "Unknown Opening";
        }

        [HttpPost("evaluate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateImage(IFormFile? image, IFormFile? groundTruth, [FromForm] string language = "English")
        {
            _logger.LogInformation("Processing image evaluation request");

            if (image == null || image.Length == 0)
            {
                _logger.LogWarning("No image file provided for evaluation");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "No image file provided" 
                });
            }

            if (groundTruth == null || groundTruth.Length == 0)
            {
                _logger.LogWarning("No ground truth file provided for evaluation");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "No ground truth file provided" 
                });
            }

            // Validate files
            if (!image.ContentType.StartsWith("image/"))
            {
                _logger.LogWarning("Uploaded image file is not an image");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Image file must be an image" 
                });
            }

            if (!groundTruth.ContentType.StartsWith("text/") && !groundTruth.FileName.EndsWith(".txt") && !groundTruth.FileName.EndsWith(".pgn"))
            {
                _logger.LogWarning("Ground truth file is not a valid text file");
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Ground truth file must be a text file (.txt or .pgn)" 
                });
            }

            try
            {
                // Save files to temp locations
                var tempImagePath = Path.GetTempFileName();
                var tempGroundTruthPath = Path.GetTempFileName();
                
                try
                {
                    // Save image file
                    using (var stream = new FileStream(tempImagePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    // Save ground truth file
                    using (var stream = new FileStream(tempGroundTruthPath, FileMode.Create))
                    {
                        await groundTruth.CopyToAsync(stream);
                    }

                    // Create evaluation service
                    var evaluationLogger = _loggerFactory.CreateLogger<ImageProcessingEvaluationService>();
                    var evaluationService = new ImageProcessingEvaluationService(
                        _imageProcessingService, 
                        evaluationLogger, 
                        useRealApi: true);

                    // Run evaluation
                    var result = await evaluationService.EvaluateAsync(tempImagePath, tempGroundTruthPath, language);

                    // Return evaluation results as JSON
                    return Ok(new
                    {
                        ImageFileName = image.FileName,
                        GroundTruthFileName = groundTruth.FileName,
                        Language = language,
                        IsSuccessful = result.IsSuccessful,
                        ErrorMessage = result.ErrorMessage,
                        ProcessingTimeSeconds = result.ProcessingTime.TotalSeconds,
                        Metrics = new
                        {
                            NormalizedScore = Math.Round(result.NormalizedScore, 3),
                            ExactMatchScore = Math.Round(result.ExactMatchScore, 3),
                            PositionalAccuracy = Math.Round(result.PositionalAccuracy, 3),
                            LevenshteinDistance = result.LevenshteinDistance,
                            LongestCommonSubsequence = result.LongestCommonSubsequence
                        },
                        MoveCounts = new
                        {
                            GroundTruthMoves = result.GroundTruthMoves.Count,
                            ExtractedMoves = result.ExtractedMoves.Count
                        },
                        Moves = new
                        {
                            GroundTruth = result.GroundTruthMoves,
                            Extracted = result.ExtractedMoves
                        },
                        GeneratedPgn = result.GeneratedPgn
                    });
                }
                finally
                {
                    // Clean up temp files
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
            catch (UnauthorizedAccessException)
            {
                _logger.LogError("Unauthorized access to OpenAI API during evaluation");
                return StatusCode(StatusCodes.Status401Unauthorized, new ErrorResponse 
                { 
                    Status = StatusCodes.Status401Unauthorized, 
                    Message = "Unauthorized access to image processing service" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during image evaluation");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to evaluate image: " + ex.Message 
                });
            }
        }

        [HttpPost("mockupload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult MockUpload()
        {
            _logger.LogInformation("Processing mock upload request");

            var mockResponse = new
            {
                pgnContent = "[Date \"2025.07.29\"]\n[White \"??\"]\n[Black \"??\"]\n[Result \"*\"]\n\n1. e4 c5 \n 2. Nf3 Nc6 \n 3. d4 cxd4 \n 4. Nxd4 Nf6 \n 5. Nc3 e5 \n 6. Nf3 Be7 \n 7. Bc4 O-O \n 8. O-O a6 \n 9. Bd2 b5 \n 10. Bb3 Bb7 \n 11. Re1 d6 \n 12. Be3 Na5 \n 13. Bd5 Nxd5 \n 14. Nxd5 Nc4 \n 15. Nxe7+ Qxe7 \n 16. Bc1 Rac8 \n 17. b3 Nb6 \n 18. Be3 Nd7 \n 19. Qd3 f5 \n 20. Ng5 f4 \n 21. Nf3 fxe3 \n 22. Rxe3 Nc5 \n 23. Qe2 Rc6 \n 24. Rd1 Rfc8 \n 25. Qd2 Ne6 \n 26. c3 Qc7 \n 27. Rd3 Nf4 \n 28. Rxd6 Rxd6 \n 29. Qxd6 Qxd6 \n 30. Rxd6 Ne2+ \n 31. Kf1 Nxc3 \n 32. Rd7 Bxe4 \n 33. Nxe5 Re8 \n 34. Nf3 Bxf3 \n 35. Qxf3 Nxa2 \n 36. Kg2 a5 \n 37. Ra7 b4 \n 38. Rxa5 Nc1 \n 39. Rb5 Nd3 \n 40. Rd5 Nf4+ \n 41. Kg3 Nxd5 \n 42. f4 Rf8 \n 43. f3 Rxf1 \n 44. h3 Rd4 \n 45. f4 Rd3+ \n 46. Kg4 Rxb3 \n 47. f5 Rc3 \n 48. f6 Nxf6+ \n 49. Kg5 b3 \n 50. h4 b2 \n 51. Kf4 b1=Q \n 52. Ke5 Qe4+ \n 53. Kd6 Rc6# \n *\n",
                validation = new
                {
                    gameId = "8c433eef-b0af-4a24-aaa4-99418ba167a9",
                    moves = new object[]
                    {
                        new { moveNumber = 1, whiteMove = new { notation = "e4", normalizedNotation = "e4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "c5", normalizedNotation = "c5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 2, whiteMove = new { notation = "Nf3", normalizedNotation = "Nf3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nc6", normalizedNotation = "Nc6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 3, whiteMove = new { notation = "d4", normalizedNotation = "d4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "cxd4", normalizedNotation = "cxd4", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 4, whiteMove = new { notation = "Nxd4", normalizedNotation = "Nxd4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nf6", normalizedNotation = "Nf6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 5, whiteMove = new { notation = "Nc3", normalizedNotation = "Nc3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "e5", normalizedNotation = "e5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 6, whiteMove = new { notation = "Nf3", normalizedNotation = "Nf3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Be7", normalizedNotation = "Be7", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 7, whiteMove = new { notation = "Bc4", normalizedNotation = "Bc4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "O-O", normalizedNotation = "O-O", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 8, whiteMove = new { notation = "O-O", normalizedNotation = "O-O", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "a6", normalizedNotation = "a6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 9, whiteMove = new { notation = "Bd2", normalizedNotation = "Bd2", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "b5", normalizedNotation = "b5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 10, whiteMove = new { notation = "Bb3", normalizedNotation = "Bb3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Bb7", normalizedNotation = "Bb7", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 11, whiteMove = new { notation = "Re1", normalizedNotation = "Re1", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "d6", normalizedNotation = "d6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 12, whiteMove = new { notation = "Be3", normalizedNotation = "Be3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Na5", normalizedNotation = "Na5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 13, whiteMove = new { notation = "Bd5", normalizedNotation = "Bd5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nxd5", normalizedNotation = "Nxd5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 14, whiteMove = new { notation = "Nxd5", normalizedNotation = "Nxd5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nc4", normalizedNotation = "Nc4", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 15, whiteMove = new { notation = "Nxe7+", normalizedNotation = "Nxe7+", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Qxe7", normalizedNotation = "Qxe7", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 16, whiteMove = new { notation = "Bc1", normalizedNotation = "Bc1", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rac8", normalizedNotation = "Rac8", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 17, whiteMove = new { notation = "b3", normalizedNotation = "b3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nb6", normalizedNotation = "Nb6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 18, whiteMove = new { notation = "Be3", normalizedNotation = "Be3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nd7", normalizedNotation = "Nd7", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 19, whiteMove = new { notation = "Qd3", normalizedNotation = "Qd3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "f5", normalizedNotation = "f5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 20, whiteMove = new { notation = "Ng5", normalizedNotation = "Ng5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "f4", normalizedNotation = "f4", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 21, whiteMove = new { notation = "Nf3", normalizedNotation = "Nf3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "fxe3", normalizedNotation = "fxe3", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 22, whiteMove = new { notation = "Rxe3", normalizedNotation = "Rxe3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nc5", normalizedNotation = "Nc5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 23, whiteMove = new { notation = "Qe2", normalizedNotation = "Qe2", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rc6", normalizedNotation = "Rc6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 24, whiteMove = new { notation = "Rd1", normalizedNotation = "Rd1", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rfc8", normalizedNotation = "Rfc8", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 25, whiteMove = new { notation = "Qd2", normalizedNotation = "Qd2", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Ne6", normalizedNotation = "Ne6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 26, whiteMove = new { notation = "c3", normalizedNotation = "c3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Qc7", normalizedNotation = "Qc7", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 27, whiteMove = new { notation = "Rd3", normalizedNotation = "Rd3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nf4", normalizedNotation = "Nf4", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 28, whiteMove = new { notation = "Rxd6", normalizedNotation = "Rxd6", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rxd6", normalizedNotation = "Rxd6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 29, whiteMove = new { notation = "Qxd6", normalizedNotation = "Qxd6", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Qxd6", normalizedNotation = "Qxd6", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 30, whiteMove = new { notation = "Rxd6", normalizedNotation = "Rxd6", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Ne2+", normalizedNotation = "Ne2+", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 31, whiteMove = new { notation = "Kf1", normalizedNotation = "Kf1", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nxc3", normalizedNotation = "Nxc3", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 32, whiteMove = new { notation = "Rd7", normalizedNotation = "Rd7", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Bxe4", normalizedNotation = "Bxe4", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 33, whiteMove = new { notation = "Nxe5", normalizedNotation = "Nxe5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Re8", normalizedNotation = "Re8", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 34, whiteMove = new { notation = "Nf3", normalizedNotation = "Nf3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Bxf3", normalizedNotation = "Bxf3", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 35, whiteMove = new { notation = "Qxf3", normalizedNotation = "Qxf3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nxa2", normalizedNotation = "Nxa2", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 36, whiteMove = new { notation = "Kg2", normalizedNotation = "Kg2", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "a5", normalizedNotation = "a5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 37, whiteMove = new { notation = "Ra7", normalizedNotation = "Ra7", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "b4", normalizedNotation = "b4", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 38, whiteMove = new { notation = "Rxa5", normalizedNotation = "Rxa5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nc1", normalizedNotation = "Nc1", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 39, whiteMove = new { notation = "Rb5", normalizedNotation = "Rb5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nd3", normalizedNotation = "Nd3", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 40, whiteMove = new { notation = "Rd5", normalizedNotation = "Rd5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nf4+", normalizedNotation = "Nf4+", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 41, whiteMove = new { notation = "Kg3", normalizedNotation = "Kg3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nxd5", normalizedNotation = "Nxd5", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 42, whiteMove = new { notation = "f4", normalizedNotation = "f4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rf8", normalizedNotation = "Rf8", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 43, whiteMove = new { notation = "f3", normalizedNotation = "f3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rxf1", normalizedNotation = "Rxf1", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 44, whiteMove = new { notation = "h3", normalizedNotation = "h3", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rd4", normalizedNotation = "Rd4", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 45, whiteMove = new { notation = "f4", normalizedNotation = "f4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rd3+", normalizedNotation = "Rd3+", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 46, whiteMove = new { notation = "Kg4", normalizedNotation = "Kg4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rxb3", normalizedNotation = "Rxb3", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 47, whiteMove = new { notation = "f5", normalizedNotation = "f5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rc3", normalizedNotation = "Rc3", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 48, whiteMove = new { notation = "f6", normalizedNotation = "f6", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Nxf6+", normalizedNotation = "Nxf6+", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 49, whiteMove = new { notation = "Kg5", normalizedNotation = "Kg5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "b3", normalizedNotation = "b3", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 50, whiteMove = new { notation = "h4", normalizedNotation = "h4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "b2", normalizedNotation = "b2", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 51, whiteMove = new { notation = "Kf4", normalizedNotation = "Kf4", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "b1=Q", normalizedNotation = "b1=Q", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 52, whiteMove = new { notation = "Ke5", normalizedNotation = "Ke5", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Qe4+", normalizedNotation = "Qe4+", validationStatus = "valid", validationText = "" } },
                        new { moveNumber = 53, whiteMove = new { notation = "Kd6", normalizedNotation = "Kd6", validationStatus = "valid", validationText = "" }, blackMove = new { notation = "Rc6#", normalizedNotation = "Rc6#", validationStatus = "valid", validationText = "" } }
                    }
                }
            };

            return Ok(mockResponse);
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