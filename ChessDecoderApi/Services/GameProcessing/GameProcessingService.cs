using ChessDecoderApi.DTOs;
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
    private readonly IProjectService _projectService;
    private readonly RepositoryFactory _repositoryFactory;
    private readonly ILogger<GameProcessingService> _logger;

    public GameProcessingService(
        IAuthService authService,
        ICreditService creditService,
        ICloudStorageService cloudStorageService,
        IImageExtractionService imageExtractionService,
        IImageManipulationService imageManipulationService,
        Services.IImageProcessingService legacyImageProcessingService,
        IProjectService projectService,
        RepositoryFactory repositoryFactory,
        ILogger<GameProcessingService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _creditService = creditService ?? throw new ArgumentNullException(nameof(creditService));
        _cloudStorageService = cloudStorageService ?? throw new ArgumentNullException(nameof(cloudStorageService));
        _imageExtractionService = imageExtractionService ?? throw new ArgumentNullException(nameof(imageExtractionService));
        _imageManipulationService = imageManipulationService ?? throw new ArgumentNullException(nameof(imageManipulationService));
        _legacyImageProcessingService = legacyImageProcessingService ?? throw new ArgumentNullException(nameof(legacyImageProcessingService));
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
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

        // Create PGN metadata from request if any field is provided
        PgnMetadata? pgnMetadata = null;
        if (!string.IsNullOrWhiteSpace(request.WhitePlayer) || 
            !string.IsNullOrWhiteSpace(request.BlackPlayer) || 
            request.GameDate.HasValue || 
            !string.IsNullOrWhiteSpace(request.Round))
        {
            pgnMetadata = new PgnMetadata
            {
                // Only set fields that are actually provided (non-null/non-empty)
                WhitePlayer = string.IsNullOrWhiteSpace(request.WhitePlayer) ? null : request.WhitePlayer,
                BlackPlayer = string.IsNullOrWhiteSpace(request.BlackPlayer) ? null : request.BlackPlayer,
                GameDate = request.GameDate,
                Round = string.IsNullOrWhiteSpace(request.Round) ? null : request.Round
            };
        }

        var startTime = DateTime.UtcNow;
        var result = await _imageExtractionService.ProcessImageAsync(imagePathForProcessing, pgnMetadata);
        var processingTime = DateTime.UtcNow - startTime;

        // Generate processed image
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePathForProcessing);
            processedImageBase64 = Convert.ToBase64String(imageBytes);
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
            Description = $"Processed chess game from image upload (language auto-detected)",
            PgnContent = result.PgnContent ?? "",
            ProcessedAt = DateTime.UtcNow,
            ProcessingTimeMs = (int)processingTime.TotalMilliseconds,
            IsValid = result.Validation?.Moves?.All(m => 
                (m.WhiteMove?.ValidationStatus == "valid" || m.WhiteMove?.ValidationStatus == "warning") &&
                (m.BlackMove?.ValidationStatus == "valid" || m.BlackMove?.ValidationStatus == "warning")) ?? false,
            WhitePlayer = request.WhitePlayer,
            BlackPlayer = request.BlackPlayer,
            GameDate = request.GameDate,
            Round = request.Round
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

        // Create project history for version tracking
        try
        {
            var uploadData = new InitialUploadData
            {
                FileName = request.Image.FileName,
                FileSize = request.Image.Length,
                FileType = request.Image.ContentType,
                UploadedAt = DateTime.UtcNow,
                StorageLocation = isStoredInCloud ? "cloud" : "local",
                StorageUrl = cloudStorageUrl
            };

            var processingDataForHistory = new ProcessingData
            {
                ProcessedAt = chessGame.ProcessedAt,
                PgnContent = result.PgnContent ?? "",
                ValidationStatus = chessGame.IsValid ? "valid" : "invalid",
                ProcessingTimeMs = (int)processingTime.TotalMilliseconds
            };

            await _projectService.CreateProjectAsync(chessGame.Id, request.UserId, uploadData, processingDataForHistory);
        }
        catch (Exception ex)
        {
            // Log but don't fail the request if project history creation fails
            _logger.LogWarning(ex, "Failed to create project history for game {GameId}, continuing...", chessGame.Id);
        }

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

    public Task<GameProcessingResponse> ProcessMockUploadAsync(IFormFile image, bool autoCrop = false)
    {
        _logger.LogInformation("Processing mock upload - returning hardcoded mock response");

        return Task.FromResult(GetMockGameProcessingResponse());
    }

    private static GameProcessingResponse GetMockGameProcessingResponse()
    {
        var mockPgn = "[Date \"????.??.??\"]\n[White \"?\"]\n[Black \"?\"]\n[Result \"*\"]\n\n1. e4 c5 \n 2. Nf3 Nc6 \n 3. d4 cxd4 \n 4. Nxd4 Nf6 \n 5. Nc3 e5 \n 6. Nf3 Be7 \n 7. Bc4 O-O \n 8. O-O a6 \n 9. Bd2 b5 \n 10. Bb3 Bb7 \n 11. Re1 d6 \n 12. Be3 Na5 \n 13. Bd5 Nxd5 \n 14. Nxd5 Nc4 \n 15. Nxe7+ Qxe7 \n 16. Bc1 Rac8 \n 17. b3 Nb6 \n 18. Be3 Nd7 \n 19. Qd3 f5 \n 20. Ng5 f4 \n 21. Nf3 fxe3 \n 22. Rxe3 Nc5 \n 23. Qe2 Rc6 \n 24. Rd1 Rfc8 \n 25. Qd2 Ne6 \n 26. c3 Qc7 \n 27. Rd3 Nf4 \n 28. Rxd6 Rxd6 \n 29. Qxd6 Qxd6 \n 30. Rxd6 Ne2+ \n 31. Kf1 Nxc3 \n 32. Rd7 Bxe4 \n 33. Nxe5 Re8 \n 34. Nf3 Bxf3 \n 35. gxf3 Nxa2 \n 36. Kg2 a5 \n 37. Ra7 b4 \n 38. Rxa5 Nc1 \n 39. Rb5 Nd3 \n 40. Rd5 Nf4+ \n 41. Kg3 Nxd5 \n 42. f4 Rf8 \n 43. f3 Rxf4 \n 44. h3 Rd4 \n 45. f4 Rd3+ \n 46. Kg4 Rxb3 \n 47. f5 Rc3 \n 48. f6 Nxf6+ \n 49. Kg5 b3 \n 50. h4 b2 \n 51. Kf4 b1=Q \n 52. Ke5 Qe4+ \n 53. Kd6 Rc6# \n *\n";

        var mockMoves = new List<ChessMovePair>
        {
            CreateMovePair(1, "e4", "c5"),
            CreateMovePair(2, "Nf3", "Nc6"),
            CreateMovePair(3, "d4", "cxd4"),
            CreateMovePair(4, "Nxd4", "Nf6"),
            CreateMovePair(5, "Nc3", "e5"),
            CreateMovePair(6, "Nf3", "Be7"),
            CreateMovePair(7, "Bc4", "O-O"),
            CreateMovePair(8, "O-O", "a6"),
            CreateMovePair(9, "Bd2", "b5"),
            CreateMovePair(10, "Bb3", "Bb7"),
            CreateMovePair(11, "Re1", "d6"),
            CreateMovePair(12, "Be3", "Na5"),
            CreateMovePair(13, "Bd5", "Nxd5"),
            CreateMovePair(14, "Nxd5", "Nc4"),
            CreateMovePair(15, "Nxe7+", "Qxe7"),
            CreateMovePair(16, "Bc1", "Rac8"),
            CreateMovePair(17, "b3", "Nb6"),
            CreateMovePair(18, "Be3", "Nd7"),
            CreateMovePair(19, "Qd3", "f5"),
            CreateMovePair(20, "Ng5", "f4"),
            CreateMovePair(21, "Nf3", "fxe3"),
            CreateMovePair(22, "Rxe3", "Nc5"),
            CreateMovePair(23, "Qe2", "Rc6"),
            CreateMovePair(24, "Rd1", "Rfc8"),
            CreateMovePair(25, "Qd2", "Ne6"),
            CreateMovePair(26, "c3", "Qc7"),
            CreateMovePair(27, "Rd3", "Nf4"),
            CreateMovePair(28, "Rxd6", "Rxd6"),
            CreateMovePair(29, "Qxd6", "Qxd6"),
            CreateMovePair(30, "Rxd6", "Ne2+"),
            CreateMovePair(31, "Kf1", "Nxc3"),
            CreateMovePair(32, "Rd7", "Bxe4"),
            CreateMovePair(33, "Nxe5", "Re8"),
            CreateMovePair(34, "Nf3", "Bxf3"),
            CreateMovePair(35, "gxf3", "Nxa2"),
            CreateMovePair(36, "Kg2", "a5"),
            CreateMovePair(37, "Ra7", "b4"),
            CreateMovePair(38, "Rxa5", "Nc1"),
            CreateMovePair(39, "Rb5", "Nd3"),
            CreateMovePair(40, "Rd5", "Nf4+"),
            CreateMovePair(41, "Kg3", "Nxd5"),
            CreateMovePair(42, "f4", "Rf8"),
            CreateMovePair(43, "f3", "Rxf4"),
            CreateMovePair(44, "h3", "Rd4"),
            CreateMovePair(45, "f4", "Rd3+"),
            CreateMovePair(46, "Kg4", "Rxb3"),
            CreateMovePair(47, "f5", "Rc3"),
            CreateMovePair(48, "f6", "Nxf6+"),
            CreateMovePair(49, "Kg5", "b3"),
            CreateMovePair(50, "h4", "b2"),
            CreateMovePair(51, "Kf4", "b1=Q"),
            CreateMovePair(52, "Ke5", "Qe4+"),
            CreateMovePair(53, "Kd6", "Rc6#")
        };

        return new GameProcessingResponse
        {
            GameId = Guid.Parse("9e0c1155-6c36-43e6-9160-7e00546f9c87"),
            PgnContent = mockPgn,
            Validation = new ChessGameValidation
            {
                GameId = "a013494e-e64a-47af-9d96-7b727f4f0b69",
                Moves = mockMoves
            },
            ProcessingTimeMs = 0,
            CreditsRemaining = 100,
            ProcessedImageUrl = null
        };
    }

    private static ChessMovePair CreateMovePair(int moveNumber, string whiteNotation, string blackNotation)
    {
        return new ChessMovePair
        {
            MoveNumber = moveNumber,
            WhiteMove = new Models.ValidatedMove
            {
                Notation = whiteNotation,
                NormalizedNotation = whiteNotation,
                ValidationStatus = "valid",
                ValidationText = ""
            },
            BlackMove = new Models.ValidatedMove
            {
                Notation = blackNotation,
                NormalizedNotation = blackNotation,
                ValidationStatus = "valid",
                ValidationText = ""
            }
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

