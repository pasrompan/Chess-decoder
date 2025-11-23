using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Services.ImageProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for handling the full game processing workflow
/// </summary>
public class GameProcessingService : IGameProcessingService
{
    private readonly IAuthService _authService;
    private readonly ICreditService _creditService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IImageExtractionService _imageExtractionService;
    private readonly IImageManipulationService _imageManipulationService;
    private readonly Services.IImageProcessingService _legacyImageProcessingService;
    private readonly RepositoryFactory _repositoryFactory;
    private readonly ILogger<GameProcessingService> _logger;

    public GameProcessingService(
        IAuthService authService,
        ICreditService creditService,
        ICloudStorageService cloudStorageService,
        IImageExtractionService imageExtractionService,
        IImageManipulationService imageManipulationService,
        Services.IImageProcessingService legacyImageProcessingService,
        RepositoryFactory repositoryFactory,
        ILogger<GameProcessingService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _creditService = creditService ?? throw new ArgumentNullException(nameof(creditService));
        _cloudStorageService = cloudStorageService ?? throw new ArgumentNullException(nameof(cloudStorageService));
        _imageExtractionService = imageExtractionService ?? throw new ArgumentNullException(nameof(imageExtractionService));
        _imageManipulationService = imageManipulationService ?? throw new ArgumentNullException(nameof(imageManipulationService));
        _legacyImageProcessingService = legacyImageProcessingService ?? throw new ArgumentNullException(nameof(legacyImageProcessingService));
        _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameProcessingResponse> ProcessGameUploadAsync(GameUploadRequest request)
    {
        _logger.LogInformation("Processing game upload for user {UserId} with autoCrop: {AutoCrop}", 
            request.UserId, request.AutoCrop);

        // Check if user exists
        var user = await _authService.GetUserProfileAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Check credits
        if (!await _creditService.HasEnoughCreditsAsync(request.UserId, 1))
        {
            throw new InvalidOperationException("Insufficient credits. Please purchase more credits to process images.");
        }

        var fileName = $"{Guid.NewGuid()}_{request.Image.FileName}";
        string filePath = string.Empty;
        string? cloudStorageUrl = null;
        string? cloudStorageObjectName = null;
        bool isStoredInCloud = false;

        // Try to upload to Cloud Storage
        try
        {
            using var imageStream = new MemoryStream();
            await request.Image.CopyToAsync(imageStream);
            imageStream.Position = 0;
            
            cloudStorageObjectName = await _cloudStorageService.UploadGameImageAsync(
                imageStream, 
                fileName, 
                request.Image.ContentType);
            
            cloudStorageUrl = await _cloudStorageService.GetImageUrlAsync(cloudStorageObjectName);
            isStoredInCloud = true;
            
            _logger.LogInformation("Image uploaded to Cloud Storage: {CloudStorageUrl}", cloudStorageUrl);
            
            // Test accessibility
            using var testClient = new HttpClient();
            var testResponse = await testClient.GetAsync(cloudStorageUrl);
            if (!testResponse.IsSuccessStatusCode)
            {
                throw new Exception("Cloud Storage image not accessible");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload to Cloud Storage, falling back to local storage");
            
            // Fallback to local storage
            cloudStorageObjectName = null;
            cloudStorageUrl = null;
            isStoredInCloud = false;
            
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }
            
            filePath = Path.Combine(uploadsDir, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.Image.CopyToAsync(stream);
            }
            
            _logger.LogInformation("Image saved locally to: {FilePath}", filePath);
        }

        var imagePathForProcessing = isStoredInCloud ? cloudStorageUrl! : filePath;
        string? processedImageBase64 = null;

        // Handle autoCrop if enabled
        if (request.AutoCrop && !isStoredInCloud)
        {
            _logger.LogInformation("Auto-crop enabled, finding table boundaries and cropping image");
            
            using var originalImage = Image.Load<Rgba32>(filePath);
            var tableBoundaries = _legacyImageProcessingService.FindTableBoundaries(originalImage);
            
            var croppedImageBytes = await _imageManipulationService.CropImageAsync(
                filePath, 
                tableBoundaries.X, 
                tableBoundaries.Y, 
                tableBoundaries.Width, 
                tableBoundaries.Height);

            var croppedFileName = $"{Guid.NewGuid()}_cropped{Path.GetExtension(request.Image.FileName)}";
            var croppedFilePath = Path.Combine(Path.GetTempPath(), croppedFileName);
            await File.WriteAllBytesAsync(croppedFilePath, croppedImageBytes);
            
            imagePathForProcessing = croppedFilePath;
        }

        var startTime = DateTime.UtcNow;
        var result = await _imageExtractionService.ProcessImageAsync(imagePathForProcessing, request.Language, request.ExpectedColumns);
        var processingTime = DateTime.UtcNow - startTime;

        // Generate processed image with boundaries
        try
        {
            if (request.AutoCrop)
            {
                var imageWithBoundaries = await _imageManipulationService.CreateImageWithBoundariesAsync(imagePathForProcessing, request.ExpectedColumns);
                processedImageBase64 = Convert.ToBase64String(imageWithBoundaries);
            }
            else
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePathForProcessing);
                processedImageBase64 = Convert.ToBase64String(imageBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate processed image");
        }

        // Create game records
        var chessGame = new ChessGame
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = $"Chess Game - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            Description = $"Processed chess game from image upload. Language: {request.Language}",
            PgnContent = result.PgnContent ?? "",
            ProcessedAt = DateTime.UtcNow,
            ProcessingTimeMs = (int)processingTime.TotalMilliseconds,
            IsValid = result.Validation?.Moves?.All(m => 
                (m.WhiteMove?.ValidationStatus == "valid" || m.WhiteMove?.ValidationStatus == "warning") &&
                (m.BlackMove?.ValidationStatus == "valid" || m.BlackMove?.ValidationStatus == "warning")) ?? false
        };

        var gameImage = new GameImage
        {
            Id = Guid.NewGuid(),
            ChessGameId = chessGame.Id,
            FileName = fileName,
            FilePath = filePath,
            FileType = request.Image.ContentType,
            FileSizeBytes = request.Image.Length,
            CloudStorageUrl = cloudStorageUrl,
            CloudStorageObjectName = cloudStorageObjectName,
            IsStoredInCloud = isStoredInCloud,
            UploadedAt = DateTime.UtcNow
        };

        var totalMoves = result.Validation?.Moves?.Count ?? 0;
        var validMoves = result.Validation?.Moves?.Count(m => 
            (m.WhiteMove?.ValidationStatus == "valid" || m.WhiteMove?.ValidationStatus == "warning") &&
            (m.BlackMove?.ValidationStatus == "valid" || m.BlackMove?.ValidationStatus == "warning")) ?? 0;

        var gameStats = new GameStatistics
        {
            Id = Guid.NewGuid(),
            ChessGameId = chessGame.Id,
            TotalMoves = totalMoves,
            ValidMoves = validMoves,
            InvalidMoves = totalMoves - validMoves,
            Opening = ExtractOpening(result.PgnContent ?? ""),
            Result = "In Progress"
        };

        // Deduct credits
        await _creditService.DeductCreditsAsync(request.UserId, 1);

        // Save to database using repositories
        var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
        var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
        var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

        await gameRepo.CreateAsync(chessGame);
        await imageRepo.CreateAsync(gameImage);
        await statsRepo.CreateAsync(gameStats);

        _logger.LogInformation("Successfully processed game {GameId} for user {UserId}", chessGame.Id, request.UserId);

        // Clean up temp files
        if (request.AutoCrop && !isStoredInCloud && imagePathForProcessing != filePath && File.Exists(imagePathForProcessing))
        {
            try
            {
                File.Delete(imagePathForProcessing);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp file: {FilePath}", imagePathForProcessing);
            }
        }

        return new GameProcessingResponse
        {
            GameId = chessGame.Id,
            PgnContent = result.PgnContent,
            Validation = result.Validation,
            ProcessingTimeMs = processingTime.TotalMilliseconds,
            CreditsRemaining = await _creditService.GetUserCreditsAsync(request.UserId),
            ProcessedImageUrl = processedImageBase64 != null ? $"data:image/png;base64,{processedImageBase64}" : null
        };
    }

    public async Task<GameProcessingResponse> ProcessMockUploadAsync(IFormFile image, string language = "English", bool autoCrop = false, int expectedColumns = 4)
    {
        _logger.LogInformation("Processing mock upload with autoCrop: {AutoCrop}, expectedColumns: {ExpectedColumns}", autoCrop, expectedColumns);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
        var tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

        using (var stream = new FileStream(tempFilePath, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }

        string imagePathForProcessing = tempFilePath;
        string? processedImageBase64 = null;

        if (autoCrop)
        {
            using var originalImage = Image.Load<Rgba32>(tempFilePath);
            var tableBoundaries = _legacyImageProcessingService.FindTableBoundaries(originalImage);
            
            var croppedImageBytes = await _imageManipulationService.CropImageAsync(
                tempFilePath, 
                tableBoundaries.X, 
                tableBoundaries.Y, 
                tableBoundaries.Width, 
                tableBoundaries.Height);

            var croppedFileName = $"{Guid.NewGuid()}_cropped{Path.GetExtension(image.FileName)}";
            var croppedFilePath = Path.Combine(Path.GetTempPath(), croppedFileName);
            await File.WriteAllBytesAsync(croppedFilePath, croppedImageBytes);
            
            imagePathForProcessing = croppedFilePath;
        }

        var result = await _imageExtractionService.ProcessImageAsync(imagePathForProcessing, language, expectedColumns);

        // Generate image
        if (autoCrop)
        {
            var imageWithBoundaries = await _imageManipulationService.CreateImageWithBoundariesAsync(imagePathForProcessing, expectedColumns);
            processedImageBase64 = Convert.ToBase64String(imageWithBoundaries);
        }
        else
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePathForProcessing);
            processedImageBase64 = Convert.ToBase64String(imageBytes);
        }

        // Clean up
        try
        {
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
            if (autoCrop && imagePathForProcessing != tempFilePath && File.Exists(imagePathForProcessing)) 
                File.Delete(imagePathForProcessing);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp files");
        }

        return new GameProcessingResponse
        {
            GameId = Guid.NewGuid(),
            PgnContent = result.PgnContent,
            Validation = result.Validation,
            ProcessingTimeMs = 0,
            CreditsRemaining = 100,
            ProcessedImageUrl = $"data:image/png;base64,{processedImageBase64}"
        };
    }

    private string ExtractOpening(string pgnContent)
    {
        var moves = pgnContent.Split('\n')
            .Where(line => !line.StartsWith('[') && !string.IsNullOrWhiteSpace(line))
            .SelectMany(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(move => !string.IsNullOrWhiteSpace(move) && !move.Contains('.'))
            .Take(6)
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
}

