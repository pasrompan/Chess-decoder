using ChessDecoderApi.Models;
using ChessDecoderApi.Services;
using ChessDecoderApi.Data;
using ChessDecoderApi.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
            [FromForm] string userId = "",
            [FromForm] bool autoCrop = false)
        {
            _logger.LogInformation("Processing image upload request (v2) for user: {UserId} with autoCrop: {AutoCrop}", userId, autoCrop);

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
                // Check if user exists first
                var dbContext = HttpContext.RequestServices.GetRequiredService<ChessDecoderDbContext>();
                var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogWarning("User {UserId} not found when processing image upload", userId);
                    return NotFound(new ErrorResponse 
                    { 
                        Status = StatusCodes.Status404NotFound, 
                        Message = "User not found" 
                    });
                }

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
                string? processedImageBase64 = null;

                // If autoCrop is enabled, first find table boundaries and crop
                if (autoCrop && !isStoredInCloud) // Only crop local files for now
                {
                    _logger.LogInformation("Auto-crop enabled, finding table boundaries and cropping image");
                    
                    // Find table boundaries
                    using var originalImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(filePath);
                    var tableBoundaries = _imageProcessingService.FindTableBoundaries(originalImage);
                    
                    _logger.LogInformation("Table boundaries found: {X}, {Y}, {Width}, {Height}", 
                        tableBoundaries.X, tableBoundaries.Y, tableBoundaries.Width, tableBoundaries.Height);

                    // Crop the image to table boundaries
                    var croppedImageBytes = await _imageProcessingService.CropImageAsync(
                        filePath, 
                        tableBoundaries.X, 
                        tableBoundaries.Y, 
                        tableBoundaries.Width, 
                        tableBoundaries.Height);

                    // Save cropped image to a new temp file
                    var croppedFileName = $"{Guid.NewGuid()}_cropped{Path.GetExtension(image.FileName)}";
                    var croppedFilePath = Path.Combine(Path.GetTempPath(), croppedFileName);
                    await System.IO.File.WriteAllBytesAsync(croppedFilePath, croppedImageBytes);
                    
                    // Use cropped image for processing
                    imagePathForProcessing = croppedFilePath;
                }

                var startTime = DateTime.UtcNow;
                var result = await _imageProcessingService.ProcessImageAsync(imagePathForProcessing, language);
                var processingTime = DateTime.UtcNow - startTime;

                // Generate image with column boundaries as red indicators
                try
                {
                    var imageWithBoundaries = await _imageProcessingService.CreateImageWithBoundariesAsync(imagePathForProcessing, 6);
                    processedImageBase64 = Convert.ToBase64String(imageWithBoundaries);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate image with boundaries, continuing without visual feedback");
                }

                // Reuse the database context from earlier

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

                // Clean up temp cropped file if created
                if (autoCrop && !isStoredInCloud && imagePathForProcessing != filePath && System.IO.File.Exists(imagePathForProcessing))
                {
                    try
                    {
                        System.IO.File.Delete(imagePathForProcessing);
                        _logger.LogInformation("Cleaned up temporary cropped file: {FilePath}", imagePathForProcessing);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up temporary cropped file: {FilePath}", imagePathForProcessing);
                    }
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
                    creditsRemaining = await creditService.GetUserCreditsAsync(userId),
                    processedImageUrl = processedImageBase64 != null ? $"data:image/png;base64,{processedImageBase64}" : null
                });
            }
            catch (UserNotFoundException ex)
            {
                _logger.LogWarning("User {UserId} not found when processing image upload", ex.UserId);
                return NotFound(new ErrorResponse 
                { 
                    Status = StatusCodes.Status404NotFound, 
                    Message = "User not found" 
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
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MockUpload(
            IFormFile? image, 
            [FromForm] string language = "English",
            [FromForm] string userId = "",
            [FromForm] bool autoCrop = false)
        {
            _logger.LogInformation("Processing mock upload request with autoCrop: {AutoCrop}", autoCrop);

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
                // Create temp file path
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                var tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

                // Save uploaded file to temp location
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                string imagePathForProcessing = tempFilePath;
                string? processedImageBase64 = null;

                // If autoCrop is enabled, first find table boundaries and crop
                if (autoCrop)
                {
                    _logger.LogInformation("Auto-crop enabled, finding table boundaries and cropping image");
                    
                    // Find table boundaries
                    using var originalImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(tempFilePath);
                    var tableBoundaries = _imageProcessingService.FindTableBoundaries(originalImage);
                    
                    _logger.LogInformation("Table boundaries found: {X}, {Y}, {Width}, {Height}", 
                        tableBoundaries.X, tableBoundaries.Y, tableBoundaries.Width, tableBoundaries.Height);

                    // Crop the image to table boundaries
                    var croppedImageBytes = await _imageProcessingService.CropImageAsync(
                        tempFilePath, 
                        tableBoundaries.X, 
                        tableBoundaries.Y, 
                        tableBoundaries.Width, 
                        tableBoundaries.Height);

                    // Save cropped image to a new temp file
                    var croppedFileName = $"{Guid.NewGuid()}_cropped{Path.GetExtension(image.FileName)}";
                    var croppedFilePath = Path.Combine(Path.GetTempPath(), croppedFileName);
                    await System.IO.File.WriteAllBytesAsync(croppedFilePath, croppedImageBytes);
                    
                    // Use cropped image for processing
                    imagePathForProcessing = croppedFilePath;
                }

                // Process the image (cropped or original)
                var result = await _imageProcessingService.ProcessImageAsync(imagePathForProcessing, language);

                // Generate image with column boundaries as red indicators
                var imageWithBoundaries = await _imageProcessingService.CreateImageWithBoundariesAsync(imagePathForProcessing, 6);
                processedImageBase64 = Convert.ToBase64String(imageWithBoundaries);

                // Clean up temp files
                try
                {
                    if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
                    if (autoCrop && imagePathForProcessing != tempFilePath && System.IO.File.Exists(imagePathForProcessing)) 
                        System.IO.File.Delete(imagePathForProcessing);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp files");
                }

                var response = new
                {
                    pgnContent = result.PgnContent,
                    validation = result.Validation,
                    gameId = Guid.NewGuid().ToString(),
                    processingTime = 0,
                    creditsRemaining = 100,
                    processedImageUrl = $"data:image/png;base64,{processedImageBase64}"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image upload with autoCrop: {AutoCrop}", autoCrop);
                return StatusCode(500, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Internal server error: " + ex.Message 
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

        [HttpPost("debug/split-columns")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DebugSplitColumns(
            IFormFile? image, 
            [FromForm] int expectedColumns = 6,
            [FromForm] int expectedRows = 0)
        {
            _logger.LogInformation("Processing debug split columns request with automatic chess column detection, expected rows: {ExpectedRows} (0 = auto-detect)", expectedRows);

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

            // Validate expected columns parameter
            if (expectedColumns < 1 || expectedColumns > 20)
            {
                _logger.LogWarning("Invalid expected columns value: {ExpectedColumns}", expectedColumns);
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Expected columns must be between 1 and 20" 
                });
            }

            // Validate expected rows parameter (0 means auto-detection)
            if (expectedRows < 0 || expectedRows > 20)
            {
                _logger.LogWarning("Invalid expected rows value: {ExpectedRows}", expectedRows);
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Expected rows must be between 0 (auto-detect) and 20" 
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

                    // Load image and find boundaries in the correct order
                    using var loadedImage = Image.Load<Rgba32>(tempFilePath);
                    
                    // Step 1: Find table boundaries first
                    var tableBoundaries = _imageProcessingService.FindTableBoundaries(loadedImage);
                    
                    // Step 2: Automatically detect chess columns within the table area
                    var columnBoundaries = _imageProcessingService.DetectChessColumnsAutomatically(loadedImage, tableBoundaries);
                    
                    // Step 3: Row processing removed - no longer needed

                    // Calculate column widths for frontend visualization
                    var columnWidths = new List<int>();
                    for (int i = 0; i < columnBoundaries.Count - 1; i++)
                    {
                        columnWidths.Add(columnBoundaries[i + 1] - columnBoundaries[i]);
                    }


                    // Return the boundaries and related data for frontend visualization
                    return Ok(new 
                    { 
                        imageFileName = image.FileName,
                        imageWidth = columnBoundaries.Last(), // Total width is the last boundary
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
                    // Clean up temp file
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing debug split columns");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to process image column splitting: " + ex.Message 
                });
            }
        }

        [HttpPost("debug/image-with-boundaries")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DebugImageWithBoundaries(
            IFormFile? image, 
            [FromForm] int expectedColumns = 6,
            [FromForm] int expectedRows = 0)
        {
            _logger.LogInformation("Processing debug image with boundaries request with expected columns: {ExpectedColumns}, expected rows: {ExpectedRows} (0 = auto-detect)", expectedColumns, expectedRows);

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

            // Validate expected columns parameter
            if (expectedColumns < 1 || expectedColumns > 20)
            {
                _logger.LogWarning("Invalid expected columns value: {ExpectedColumns}", expectedColumns);
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Expected columns must be between 1 and 20" 
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

                    // Create image with boundaries drawn
                    var imageWithBoundaries = await _imageProcessingService.CreateImageWithBoundariesAsync(tempFilePath, expectedColumns);

                    // Return the image with boundaries as JPEG
                    return File(imageWithBoundaries, "image/jpeg", $"boundaries_{image.FileName}");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing debug image with boundaries");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to process image with boundaries: " + ex.Message 
                });
            }
        }

        [HttpPost("debug/table-boundaries")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DebugTableBoundaries(IFormFile? image)
        {
            _logger.LogInformation("Processing debug table boundaries request");

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

                    // Load image and find table boundaries
                    using var loadedImage = Image.Load<Rgba32>(tempFilePath);
                    var tableBoundaries = _imageProcessingService.FindTableBoundaries(loadedImage);
                    
                    // Create image with only table boundaries
                    using var imageWithTableBoundaries = loadedImage.Clone();
                    DrawTableBoundariesOnImage(imageWithTableBoundaries, tableBoundaries);
                    
                    // Convert to byte array
                    using var ms = new MemoryStream();
                    await imageWithTableBoundaries.SaveAsJpegAsync(ms);
                    var imageBytes = ms.ToArray();

                    // Return the image with table boundaries as JPEG
                    return File(imageBytes, "image/jpeg", $"table_boundaries_{image.FileName}");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing debug table boundaries");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to process table boundaries: " + ex.Message 
                });
            }
        }

        [HttpPost("debug/crop-image")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DebugCropImage(
            IFormFile? image,
            [FromForm] int x,
            [FromForm] int y,
            [FromForm] int width,
            [FromForm] int height)
        {
            _logger.LogInformation("Processing debug crop image request with coordinates: ({X}, {Y}) and size: {Width}x{Height}", x, y, width, height);

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

            // Validate crop parameters
            if (width <= 0 || height <= 0)
            {
                _logger.LogWarning("Invalid crop dimensions: {Width}x{Height}", width, height);
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "Width and height must be positive integers" 
                });
            }

            if (x < 0 || y < 0)
            {
                _logger.LogWarning("Invalid crop coordinates: ({X}, {Y})", x, y);
                return BadRequest(new ErrorResponse 
                { 
                    Status = StatusCodes.Status400BadRequest, 
                    Message = "X and Y coordinates cannot be negative" 
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

                    // Crop the image
                    var croppedImageBytes = await _imageProcessingService.CropImageAsync(tempFilePath, x, y, width, height);

                    // Return the cropped image as JPEG
                    return File(croppedImageBytes, "image/jpeg", $"cropped_{image.FileName}");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing debug crop image");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to crop image: " + ex.Message 
                });
            }
        }

        [HttpPost("debug/table-analysis")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DebugTableAnalysis(IFormFile? image)
        {
            _logger.LogInformation("Processing debug table analysis request");

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

                    // Load image and analyze
                    using var loadedImage = Image.Load<Rgba32>(tempFilePath);
                    var tableBoundaries = _imageProcessingService.FindTableBoundaries(loadedImage);
                    
                    // Calculate some statistics
                    var imageWidth = loadedImage.Width;
                    var imageHeight = loadedImage.Height;
                    var tableArea = tableBoundaries.Width * tableBoundaries.Height;
                    var imageArea = imageWidth * imageHeight;
                    var tablePercentage = (double)tableArea / imageArea * 100;
                    
                    // Return detailed analysis
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
                    // Clean up temp file
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing debug table analysis");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse 
                { 
                    Status = StatusCodes.Status500InternalServerError, 
                    Message = "Failed to process table analysis: " + ex.Message 
                });
            }
        }

        private void DrawTableBoundariesOnImage(Image<Rgba32> image, Rectangle tableBoundaries)
        {
            var blue = new Rgba32(0, 0, 255, 255); // Blue color
            int thickness = 6; // Thicker than column boundaries
            
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
}