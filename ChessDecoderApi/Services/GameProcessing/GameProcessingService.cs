using ChessDecoderApi.DTOs;
using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Services.ImageProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using System.Text.RegularExpressions;

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
    private static readonly Regex PgnHeaderRegex = new(@"^\s*\[(?<tag>\w+)\s+""(?<value>.*?)""\]\s*$", RegexOptions.Multiline);

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
        byte[]? processedImageBytes = null;
        string? processedImageCloudStorageUrl = null;
        string? processedImageCloudStorageObjectName = null;
        string? processedImageLocalPath = null;

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
        var primaryRange = GetMoveRange(result.Validation);

        // Generate processed image
        try
        {
            processedImageBytes = await File.ReadAllBytesAsync(imagePathForProcessing);
            processedImageBase64 = Convert.ToBase64String(processedImageBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate processed image");
        }

        if (processedImageBytes != null)
        {
            try
            {
                if (isStoredInCloud)
                {
                    var extension = Path.GetExtension(request.Image.FileName);
                    var processedFileName = $"{Guid.NewGuid()}_processed{extension}";
                    using var processedStream = new MemoryStream(processedImageBytes);
                    processedImageCloudStorageObjectName = await _cloudStorageService.UploadGameImageAsync(
                        processedStream,
                        processedFileName,
                        request.Image.ContentType);
                    processedImageCloudStorageUrl = await _cloudStorageService.GetImageUrlAsync(processedImageCloudStorageObjectName);
                }
                else
                {
                    var processedDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "processed");
                    if (!Directory.Exists(processedDir))
                    {
                        Directory.CreateDirectory(processedDir);
                    }

                    var processedFileName = $"{Guid.NewGuid()}_processed{Path.GetExtension(request.Image.FileName)}";
                    processedImageLocalPath = Path.Combine(processedDir, processedFileName);
                    await File.WriteAllBytesAsync(processedImageLocalPath, processedImageBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist processed image variant for game upload");
            }
        }

        // Create game records
        var chessGame = new ChessGame
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = $"Chess Game - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            Description = $"Processed chess game from image upload (language auto-detected)",
            PgnContent = result.PgnContent ?? "",
            PrimaryPagePgnContent = result.PgnContent ?? "",
            ProcessedAt = DateTime.UtcNow,
            ProcessingTimeMs = (int)processingTime.TotalMilliseconds,
            IsValid = result.Validation?.Moves?.All(m => 
                (m.WhiteMove?.ValidationStatus == "valid" || m.WhiteMove?.ValidationStatus == "warning") &&
                (m.BlackMove?.ValidationStatus == "valid" || m.BlackMove?.ValidationStatus == "warning")) ?? false,
            WhitePlayer = request.WhitePlayer,
            BlackPlayer = request.BlackPlayer,
            GameDate = request.GameDate,
            Round = request.Round,
            HasContinuation = false
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
            UploadedAt = DateTime.UtcNow,
            Variant = "original",
            PageNumber = 1,
            StartingMoveNumber = primaryRange.StartMoveNumber,
            EndingMoveNumber = primaryRange.EndMoveNumber
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

        if (processedImageBytes != null)
        {
            try
            {
                var processedVariantImage = new GameImage
                {
                    Id = Guid.NewGuid(),
                    ChessGameId = chessGame.Id,
                    FileName = $"{Path.GetFileNameWithoutExtension(fileName)}_processed{Path.GetExtension(fileName)}",
                    FilePath = processedImageLocalPath ?? string.Empty,
                    FileType = request.Image.ContentType,
                    FileSizeBytes = processedImageBytes.Length,
                    CloudStorageUrl = processedImageCloudStorageUrl,
                    CloudStorageObjectName = processedImageCloudStorageObjectName,
                    IsStoredInCloud = isStoredInCloud && !string.IsNullOrWhiteSpace(processedImageCloudStorageObjectName),
                    UploadedAt = DateTime.UtcNow,
                    Variant = "processed",
                    PageNumber = 1,
                    StartingMoveNumber = primaryRange.StartMoveNumber,
                    EndingMoveNumber = primaryRange.EndMoveNumber
                };

                await imageRepo.CreateAsync(processedVariantImage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save processed image variant record for game {GameId}", chessGame.Id);
            }
        }

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

    public async Task<DualGameUploadResponse> ProcessDualGameUploadAsync(DualGameUploadRequest request)
    {
        _logger.LogInformation("Processing dual game upload for user {UserId}", request.UserId);

        var user = await _authService.GetUserProfileAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!await _creditService.HasEnoughCreditsAsync(request.UserId, 1))
        {
            throw new InvalidOperationException("Insufficient credits. Please purchase more credits to process images.");
        }

        var metadata = BuildMetadata(
            request.WhitePlayer,
            request.BlackPlayer,
            request.GameDate,
            request.Round);

        var page1Upload = await PersistUploadedImageAsync(request.Page1);
        var page2Upload = await PersistUploadedImageAsync(request.Page2);

        var page1ProcessingPath = await PrepareImageForProcessingAsync(page1Upload, request.Page1, request.AutoCrop);
        var page2ProcessingPath = await PrepareImageForProcessingAsync(page2Upload, request.Page2, request.AutoCrop);

        try
        {
            var startTime = DateTime.UtcNow;
            var page1Task = _imageExtractionService.ProcessImageAsync(page1ProcessingPath.PathToProcess, null);
            var page2Task = _imageExtractionService.ProcessImageAsync(page2ProcessingPath.PathToProcess, null);
            await Task.WhenAll(page1Task, page2Task);
            var processingTime = DateTime.UtcNow - startTime;

            var firstResult = page1Task.Result;
            var secondResult = page2Task.Result;

            var firstRange = GetMoveRange(firstResult.Validation, firstResult.PgnContent);
            var secondRange = GetMoveRange(secondResult.Validation, secondResult.PgnContent);

            var ambiguousSameStart = firstRange.StartMoveNumber > 0 &&
                                     secondRange.StartMoveNumber > 0 &&
                                     firstRange.StartMoveNumber == secondRange.StartMoveNumber;

            // If both pages start at the same move, keep request order and continue.
            // This lets us still return usable results from page 1 instead of failing the whole upload.
            var firstIsPage1 = ambiguousSameStart
                ? true
                : (firstRange.StartMoveNumber == 0 || secondRange.StartMoveNumber == 0
                    ? firstRange.EndMoveNumber <= secondRange.EndMoveNumber
                    : firstRange.StartMoveNumber < secondRange.StartMoveNumber);

            var page1Result = firstIsPage1 ? firstResult : secondResult;
            var page2Result = firstIsPage1 ? secondResult : firstResult;
            var page1Stored = firstIsPage1 ? page1Upload : page2Upload;
            var page2Stored = firstIsPage1 ? page2Upload : page1Upload;
            var page1RequestFile = firstIsPage1 ? request.Page1 : request.Page2;
            var page2RequestFile = firstIsPage1 ? request.Page2 : request.Page1;
            var page1Range = firstIsPage1 ? firstRange : secondRange;
            var page2Range = firstIsPage1 ? secondRange : firstRange;

            var page1Moves = ExtractMovePairs(page1Result.Validation, page1Result.PgnContent);
            var page2Moves = ExtractMovePairs(page2Result.Validation, page2Result.PgnContent);
            var mergeResult = MergePageMoves(page1Moves, page2Moves, page1Range, page2Range);
            if (ambiguousSameStart)
            {
                mergeResult.Validation.IsValid = false;
                mergeResult.Validation.Warnings.Insert(0,
                    "Both uploaded pages start at the same move number. Returned page 1 as primary and merged overlapping moves where possible.");
            }
            var mergedPgn = BuildPgnFromMovePairs(mergeResult.Moves, metadata, "*");

            var chessGame = new ChessGame
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Title = $"Chess Game - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                Description = "Processed two-page chess game upload",
                PgnContent = mergedPgn,
                PrimaryPagePgnContent = BuildPgnFromMovePairs(page1Moves, metadata, "*"),
                ProcessedAt = DateTime.UtcNow,
                ProcessingTimeMs = (int)processingTime.TotalMilliseconds,
                IsValid = true,
                WhitePlayer = request.WhitePlayer,
                BlackPlayer = request.BlackPlayer,
                GameDate = request.GameDate,
                Round = request.Round,
                HasContinuation = true
            };

            var page1Image = new GameImage
            {
                Id = Guid.NewGuid(),
                ChessGameId = chessGame.Id,
                FileName = page1Stored.FileName,
                FilePath = page1Stored.FilePath,
                FileType = page1RequestFile.ContentType,
                FileSizeBytes = page1RequestFile.Length,
                CloudStorageUrl = page1Stored.CloudStorageUrl,
                CloudStorageObjectName = page1Stored.CloudStorageObjectName,
                IsStoredInCloud = page1Stored.IsStoredInCloud,
                UploadedAt = DateTime.UtcNow,
                Variant = "original",
                PageNumber = 1,
                StartingMoveNumber = page1Range.StartMoveNumber,
                EndingMoveNumber = page1Range.EndMoveNumber
            };

            var page2Image = new GameImage
            {
                Id = Guid.NewGuid(),
                ChessGameId = chessGame.Id,
                FileName = page2Stored.FileName,
                FilePath = page2Stored.FilePath,
                FileType = page2RequestFile.ContentType,
                FileSizeBytes = page2RequestFile.Length,
                CloudStorageUrl = page2Stored.CloudStorageUrl,
                CloudStorageObjectName = page2Stored.CloudStorageObjectName,
                IsStoredInCloud = page2Stored.IsStoredInCloud,
                UploadedAt = DateTime.UtcNow,
                Variant = "original",
                PageNumber = 2,
                StartingMoveNumber = page2Range.StartMoveNumber,
                EndingMoveNumber = page2Range.EndMoveNumber
            };
            page1Image.ContinuationImageId = page2Image.Id;

            var totalMoves = mergeResult.Moves.Count(m =>
                !string.IsNullOrWhiteSpace(GetMoveNotation(m.WhiteMove)) ||
                !string.IsNullOrWhiteSpace(GetMoveNotation(m.BlackMove)));

            var gameStats = new GameStatistics
            {
                Id = Guid.NewGuid(),
                ChessGameId = chessGame.Id,
                TotalMoves = totalMoves,
                ValidMoves = totalMoves,
                InvalidMoves = 0,
                Opening = ExtractOpening(mergedPgn),
                Result = "In Progress"
            };

            await _creditService.DeductCreditsAsync(request.UserId, 1);

            var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
            var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
            var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

            await gameRepo.CreateAsync(chessGame);
            await imageRepo.CreateAsync(page1Image);
            await imageRepo.CreateAsync(page2Image);
            await statsRepo.CreateAsync(gameStats);

            try
            {
                var uploadData = new InitialUploadData
                {
                    FileName = $"{request.Page1.FileName}; {request.Page2.FileName}",
                    FileSize = request.Page1.Length + request.Page2.Length,
                    FileType = "multipart/image",
                    UploadedAt = DateTime.UtcNow,
                    StorageLocation = page1Stored.IsStoredInCloud || page2Stored.IsStoredInCloud ? "cloud" : "local",
                    StorageUrl = page1Stored.CloudStorageUrl ?? page2Stored.CloudStorageUrl
                };

                var processingDataForHistory = new ProcessingData
                {
                    ProcessedAt = chessGame.ProcessedAt,
                    PgnContent = mergedPgn,
                    ValidationStatus = "valid",
                    ProcessingTimeMs = (int)processingTime.TotalMilliseconds
                };

                await _projectService.CreateProjectAsync(chessGame.Id, request.UserId, uploadData, processingDataForHistory);
                await _projectService.AddHistoryEntryAsync(
                    chessGame.Id,
                    "update",
                    "Second page uploaded and merged automatically",
                    new Dictionary<string, object>
                    {
                        { "page1StartMove", page1Range.StartMoveNumber },
                        { "page1EndMove", page1Range.EndMoveNumber },
                        { "page2StartMove", page2Range.StartMoveNumber },
                        { "page2EndMove", page2Range.EndMoveNumber }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write project history for dual upload game {GameId}", chessGame.Id);
            }

            return new DualGameUploadResponse
            {
                GameId = chessGame.Id,
                MergedPgn = mergedPgn,
                TotalMoves = totalMoves,
                Validation = new ChessGameValidation
                {
                    GameId = chessGame.Id.ToString(),
                    Moves = mergeResult.Moves
                },
                ProcessingTimeMs = processingTime.TotalMilliseconds,
                CreditsRemaining = await _creditService.GetUserCreditsAsync(request.UserId),
                Page1 = ToPageInfo(page1Image),
                Page2 = ToPageInfo(page2Image),
                ContinuationValidation = mergeResult.Validation
            };
        }
        finally
        {
            CleanupTempFile(page1ProcessingPath.TempPathToDelete);
            CleanupTempFile(page2ProcessingPath.TempPathToDelete);
        }
    }

    public async Task<ContinuationUploadResponse> AddContinuationAsync(Guid gameId, ContinuationUploadRequest request)
    {
        _logger.LogInformation("Adding continuation for game {GameId}", gameId);

        var user = await _authService.GetUserProfileAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!await _creditService.HasEnoughCreditsAsync(request.UserId, 1))
        {
            throw new InvalidOperationException("Insufficient credits. Please purchase more credits to process images.");
        }

        var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
        var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
        var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

        var game = await gameRepo.GetByIdAsync(gameId);
        if (game == null)
        {
            throw new KeyNotFoundException("Game not found");
        }

        if (!string.Equals(game.UserId, request.UserId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException("Game not found");
        }

        if (game.HasContinuation)
        {
            throw new InvalidOperationException("Game already has a continuation page");
        }

        var existingImages = await imageRepo.GetByChessGameIdAsync(gameId);
        var page2Exists = existingImages.Any(i => i.PageNumber == 2 && string.Equals(i.Variant, "original", StringComparison.OrdinalIgnoreCase));
        if (page2Exists)
        {
            throw new InvalidOperationException("Game already has a continuation page");
        }

        var continuationUpload = await PersistUploadedImageAsync(request.Image);
        var continuationProcessingPath = await PrepareImageForProcessingAsync(continuationUpload, request.Image, request.AutoCrop);

        try
        {
            var startTime = DateTime.UtcNow;
            var continuationResult = await _imageExtractionService.ProcessImageAsync(continuationProcessingPath.PathToProcess, null);
            var processingTime = DateTime.UtcNow - startTime;

            var page1Moves = ExtractMovePairs(null, game.PgnContent);
            if (page1Moves.Count == 0)
            {
                throw new InvalidOperationException("Existing game does not contain move data to continue from");
            }

            var page2Moves = ExtractMovePairs(continuationResult.Validation, continuationResult.PgnContent);
            if (page2Moves.Count == 0)
            {
                throw new InvalidOperationException("Could not extract continuation moves from uploaded image");
            }

            var page1Range = GetMoveRange(null, game.PgnContent);
            var page2Range = GetMoveRange(continuationResult.Validation, continuationResult.PgnContent);
            if (page2Range.StartMoveNumber <= 1)
            {
                throw new InvalidOperationException("Continuation page appears to start from move 1. Please upload the first page with the standard upload flow.");
            }

            var mergeResult = MergePageMoves(page1Moves, page2Moves, page1Range, page2Range);
            var metadata = BuildMetadata(game.WhitePlayer, game.BlackPlayer, game.GameDate, game.Round);
            var gameResult = ExtractResultFromPgn(game.PgnContent);
            var mergedPgn = BuildPgnFromMovePairs(mergeResult.Moves, metadata, gameResult);

            var page2Image = new GameImage
            {
                Id = Guid.NewGuid(),
                ChessGameId = game.Id,
                FileName = continuationUpload.FileName,
                FilePath = continuationUpload.FilePath,
                FileType = request.Image.ContentType,
                FileSizeBytes = request.Image.Length,
                CloudStorageUrl = continuationUpload.CloudStorageUrl,
                CloudStorageObjectName = continuationUpload.CloudStorageObjectName,
                IsStoredInCloud = continuationUpload.IsStoredInCloud,
                UploadedAt = DateTime.UtcNow,
                Variant = "original",
                PageNumber = 2,
                StartingMoveNumber = page2Range.StartMoveNumber,
                EndingMoveNumber = page2Range.EndMoveNumber
            };

            var page1Images = existingImages.Where(i =>
                    i.PageNumber == 1 ||
                    (i.PageNumber <= 0 && string.Equals(i.Variant, "original", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var image in page1Images)
            {
                image.PageNumber = 1;
                image.ContinuationImageId = page2Image.Id;
                if (image.StartingMoveNumber == 0 || image.EndingMoveNumber == 0)
                {
                    image.StartingMoveNumber = page1Range.StartMoveNumber;
                    image.EndingMoveNumber = page1Range.EndMoveNumber;
                }
                await imageRepo.UpdateAsync(image);
            }

            await imageRepo.CreateAsync(page2Image);

            if (string.IsNullOrWhiteSpace(game.PrimaryPagePgnContent))
            {
                game.PrimaryPagePgnContent = game.PgnContent;
            }

            game.PgnContent = mergedPgn;
            game.HasContinuation = true;
            game.ProcessingTimeMs += (int)processingTime.TotalMilliseconds;
            await gameRepo.UpdateAsync(game);

            var totalMoves = mergeResult.Moves.Count(m =>
                !string.IsNullOrWhiteSpace(GetMoveNotation(m.WhiteMove)) ||
                !string.IsNullOrWhiteSpace(GetMoveNotation(m.BlackMove)));

            await statsRepo.CreateOrUpdateAsync(new GameStatistics
            {
                Id = Guid.NewGuid(),
                ChessGameId = game.Id,
                TotalMoves = totalMoves,
                ValidMoves = totalMoves,
                InvalidMoves = 0,
                Opening = ExtractOpening(mergedPgn),
                Result = gameResult == "*" ? "In Progress" : gameResult,
                CreatedAt = DateTime.UtcNow
            });

            await _creditService.DeductCreditsAsync(request.UserId, 1);

            try
            {
                await _projectService.AddHistoryEntryAsync(
                    game.Id,
                    "update",
                    "Continuation page uploaded",
                    new Dictionary<string, object>
                    {
                        { "page2StartMove", page2Range.StartMoveNumber },
                        { "page2EndMove", page2Range.EndMoveNumber },
                        { "warningsCount", mergeResult.Validation.Warnings.Count }
                    });

                await _projectService.UpdateProcessingDataAsync(game.Id, new ProcessingData
                {
                    ProcessedAt = DateTime.UtcNow,
                    PgnContent = mergedPgn,
                    ValidationStatus = "valid",
                    ProcessingTimeMs = game.ProcessingTimeMs
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update project history for continuation on game {GameId}", game.Id);
            }

            return new ContinuationUploadResponse
            {
                GameId = game.Id,
                UpdatedPgn = mergedPgn,
                TotalMoves = totalMoves,
                Validation = new ChessGameValidation
                {
                    GameId = game.Id.ToString(),
                    Moves = mergeResult.Moves
                },
                ProcessingTimeMs = processingTime.TotalMilliseconds,
                Page2 = ToPageInfo(page2Image),
                ContinuationValidation = mergeResult.Validation
            };
        }
        finally
        {
            CleanupTempFile(continuationProcessingPath.TempPathToDelete);
        }
    }

    public async Task<GamePagesResponse> GetGamePagesAsync(Guid gameId, string userId)
    {
        var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
        var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();

        var game = await gameRepo.GetByIdAsync(gameId);
        if (game == null || !string.Equals(game.UserId, userId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException("Game not found");
        }

        var images = await imageRepo.GetByChessGameIdAsync(gameId);
        var originalImages = images.Where(i => string.Equals(i.Variant, "original", StringComparison.OrdinalIgnoreCase)).ToList();
        var page1 = originalImages.FirstOrDefault(i => i.PageNumber == 1) ?? originalImages.FirstOrDefault();
        var page2 = originalImages.FirstOrDefault(i => i.PageNumber == 2);

        return new GamePagesResponse
        {
            GameId = gameId,
            HasContinuation = page2 != null || game.HasContinuation,
            Page1 = page1 != null ? ToPageInfo(page1) : null,
            Page2 = page2 != null ? ToPageInfo(page2) : null
        };
    }

    public async Task<DeleteContinuationResponse> DeleteContinuationAsync(Guid gameId, string userId)
    {
        var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
        var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
        var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

        var game = await gameRepo.GetByIdAsync(gameId);
        if (game == null || !string.Equals(game.UserId, userId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException("Game not found");
        }

        var images = await imageRepo.GetByChessGameIdAsync(gameId);
        var page2Images = images.Where(i => i.PageNumber == 2).ToList();
        if (page2Images.Count == 0)
        {
            throw new InvalidOperationException("Game has no continuation page to delete");
        }

        foreach (var page2 in page2Images)
        {
            await imageRepo.DeleteAsync(page2.Id);
        }

        var page1Images = images.Where(i => i.PageNumber == 1 || i.PageNumber == 0).ToList();
        foreach (var page1 in page1Images)
        {
            if (page1.ContinuationImageId.HasValue)
            {
                page1.ContinuationImageId = null;
                await imageRepo.UpdateAsync(page1);
            }
        }

        var restoredPgn = string.IsNullOrWhiteSpace(game.PrimaryPagePgnContent)
            ? game.PgnContent
            : game.PrimaryPagePgnContent;

        game.PgnContent = restoredPgn;
        game.HasContinuation = false;
        await gameRepo.UpdateAsync(game);

        var restoredMoves = ExtractMovePairs(null, restoredPgn);
        var totalMoves = restoredMoves.Count(m =>
            !string.IsNullOrWhiteSpace(GetMoveNotation(m.WhiteMove)) ||
            !string.IsNullOrWhiteSpace(GetMoveNotation(m.BlackMove)));

        await statsRepo.CreateOrUpdateAsync(new GameStatistics
        {
            Id = Guid.NewGuid(),
            ChessGameId = game.Id,
            TotalMoves = totalMoves,
            ValidMoves = totalMoves,
            InvalidMoves = 0,
            Opening = ExtractOpening(restoredPgn),
            Result = ExtractResultFromPgn(restoredPgn) == "*" ? "In Progress" : ExtractResultFromPgn(restoredPgn),
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _projectService.AddHistoryEntryAsync(game.Id, "update", "Continuation page removed");
            await _projectService.UpdateProcessingDataAsync(game.Id, new ProcessingData
            {
                ProcessedAt = DateTime.UtcNow,
                PgnContent = restoredPgn,
                ValidationStatus = "valid",
                ProcessingTimeMs = game.ProcessingTimeMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update project history after continuation deletion for game {GameId}", gameId);
        }

        return new DeleteContinuationResponse
        {
            GameId = gameId,
            UpdatedPgn = restoredPgn,
            TotalMoves = totalMoves,
            HasContinuation = false
        };
    }

    private static PgnMetadata? BuildMetadata(string? whitePlayer, string? blackPlayer, DateTime? gameDate, string? round)
    {
        if (string.IsNullOrWhiteSpace(whitePlayer) &&
            string.IsNullOrWhiteSpace(blackPlayer) &&
            !gameDate.HasValue &&
            string.IsNullOrWhiteSpace(round))
        {
            return null;
        }

        return new PgnMetadata
        {
            WhitePlayer = string.IsNullOrWhiteSpace(whitePlayer) ? null : whitePlayer,
            BlackPlayer = string.IsNullOrWhiteSpace(blackPlayer) ? null : blackPlayer,
            GameDate = gameDate,
            Round = string.IsNullOrWhiteSpace(round) ? null : round
        };
    }

    private async Task<StoredUploadImage> PersistUploadedImageAsync(IFormFile imageFile)
    {
        var fileName = $"{Guid.NewGuid()}_{imageFile.FileName}";
        var localFilePath = string.Empty;
        string? cloudStorageUrl = null;
        string? cloudStorageObjectName = null;
        var isStoredInCloud = false;

        try
        {
            await using var imageStream = new MemoryStream();
            await imageFile.CopyToAsync(imageStream);
            imageStream.Position = 0;

            cloudStorageObjectName = await _cloudStorageService.UploadGameImageAsync(imageStream, fileName, imageFile.ContentType);
            cloudStorageUrl = await _cloudStorageService.GetImageUrlAsync(cloudStorageObjectName);
            if (string.IsNullOrWhiteSpace(cloudStorageUrl))
            {
                throw new InvalidOperationException("Cloud Storage URL was empty");
            }

            // Keep dual/continuation behavior aligned with single upload:
            // if object exists but is not publicly readable, fall back to local file processing.
            using var accessibilityClient = new HttpClient();
            var accessibilityResponse = await accessibilityClient.GetAsync(cloudStorageUrl);
            accessibilityResponse.EnsureSuccessStatusCode();

            isStoredInCloud = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload image to cloud storage, falling back to local upload");

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            localFilePath = Path.Combine(uploadsDir, fileName);
            await using var stream = new FileStream(localFilePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);
        }

        return new StoredUploadImage(
            fileName,
            localFilePath,
            cloudStorageUrl,
            cloudStorageObjectName,
            isStoredInCloud);
    }

    private async Task<PreparedImagePath> PrepareImageForProcessingAsync(StoredUploadImage upload, IFormFile file, bool autoCrop)
    {
        var hasCloudPath = upload.IsStoredInCloud && !string.IsNullOrWhiteSpace(upload.CloudStorageUrl);
        var imagePathForProcessing = hasCloudPath ? upload.CloudStorageUrl! : upload.FilePath;
        string? tempPath = null;

        if (autoCrop && !hasCloudPath && !string.IsNullOrWhiteSpace(upload.FilePath))
        {
            try
            {
                using var originalImage = Image.Load<Rgba32>(upload.FilePath);
                var tableBoundaries = _legacyImageProcessingService.FindTableBoundaries(originalImage);
                var croppedImageBytes = await _imageManipulationService.CropImageAsync(
                    upload.FilePath,
                    tableBoundaries.X,
                    tableBoundaries.Y,
                    tableBoundaries.Width,
                    tableBoundaries.Height);

                var croppedFileName = $"{Guid.NewGuid()}_cropped{Path.GetExtension(file.FileName)}";
                tempPath = Path.Combine(Path.GetTempPath(), croppedFileName);
                await File.WriteAllBytesAsync(tempPath, croppedImageBytes);
                imagePathForProcessing = tempPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-crop failed; continuing with original image");
            }
        }

        return new PreparedImagePath(imagePathForProcessing, tempPath);
    }

    private void CleanupTempFile(string? tempPath)
    {
        if (string.IsNullOrWhiteSpace(tempPath) || !File.Exists(tempPath))
        {
            return;
        }

        try
        {
            File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean temporary file {TempPath}", tempPath);
        }
    }

    private static MoveRange GetMoveRange(ChessGameValidation? validation, string? pgnContent = null)
    {
        var moves = validation?.Moves?
            .Where(m => m.MoveNumber > 0)
            .Where(m => !string.IsNullOrWhiteSpace(GetMoveNotation(m.WhiteMove)) || !string.IsNullOrWhiteSpace(GetMoveNotation(m.BlackMove)))
            .OrderBy(m => m.MoveNumber)
            .ToList() ?? new List<ChessMovePair>();

        if (moves.Count > 0)
        {
            var first = moves[0];
            return new MoveRange(first.MoveNumber, moves[^1].MoveNumber);
        }

        if (!string.IsNullOrWhiteSpace(pgnContent))
        {
            var moveNumbers = Regex.Matches(pgnContent, @"\b(\d+)\.(?:\.\.)?")
                .Select(m => int.TryParse(m.Groups[1].Value, out var number) ? number : 0)
                .Where(n => n > 0)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (moveNumbers.Count > 0)
            {
                return new MoveRange(moveNumbers[0], moveNumbers[^1]);
            }
        }

        return new MoveRange(0, 0);
    }

    private static List<ChessMovePair> ExtractMovePairs(ChessGameValidation? validation, string? pgnContent)
    {
        var fromValidation = validation?.Moves?
            .Where(m => m.MoveNumber > 0)
            .Select(CloneMovePair)
            .Where(m => !string.IsNullOrWhiteSpace(GetMoveNotation(m.WhiteMove)) || !string.IsNullOrWhiteSpace(GetMoveNotation(m.BlackMove)))
            .OrderBy(m => m.MoveNumber)
            .ToList();

        if (fromValidation != null && fromValidation.Count > 0)
        {
            return fromValidation;
        }

        return ParseMovePairsFromPgn(pgnContent);
    }

    private static MergeResult MergePageMoves(
        IReadOnlyCollection<ChessMovePair> page1Moves,
        IReadOnlyCollection<ChessMovePair> page2Moves,
        MoveRange page1Range,
        MoveRange page2Range)
    {
        var warnings = new List<string>();
        var hasGap = false;
        var hasOverlap = false;
        int? gapSize = null;
        int? overlapMoves = null;

        if (page1Range.EndMoveNumber > 0 && page2Range.StartMoveNumber > 0)
        {
            if (page2Range.StartMoveNumber > page1Range.EndMoveNumber + 1)
            {
                hasGap = true;
                gapSize = page2Range.StartMoveNumber - (page1Range.EndMoveNumber + 1);
                warnings.Add($"Detected a gap of {gapSize} move(s) between page 1 and page 2.");
            }
            else if (page2Range.StartMoveNumber <= page1Range.EndMoveNumber)
            {
                hasOverlap = true;
                overlapMoves = page1Range.EndMoveNumber - page2Range.StartMoveNumber + 1;
                warnings.Add($"Detected overlap of {overlapMoves} move(s). Duplicate moves were deduplicated.");
            }
        }

        var merged = page1Moves
            .GroupBy(m => m.MoveNumber)
            .ToDictionary(g => g.Key, g => CloneMovePair(g.First()));

        foreach (var incoming in page2Moves.OrderBy(m => m.MoveNumber))
        {
            if (!merged.TryGetValue(incoming.MoveNumber, out var existing))
            {
                merged[incoming.MoveNumber] = CloneMovePair(incoming);
                continue;
            }

            var incomingWhite = GetMoveNotation(incoming.WhiteMove);
            var incomingBlack = GetMoveNotation(incoming.BlackMove);
            var existingWhite = GetMoveNotation(existing.WhiteMove);
            var existingBlack = GetMoveNotation(existing.BlackMove);

            if (string.IsNullOrWhiteSpace(existingWhite) && !string.IsNullOrWhiteSpace(incomingWhite))
            {
                existing.WhiteMove = CloneMove(incoming.WhiteMove);
            }
            else if (!string.IsNullOrWhiteSpace(existingWhite) && !string.IsNullOrWhiteSpace(incomingWhite) &&
                     !string.Equals(existingWhite, incomingWhite, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Move {incoming.MoveNumber} white differs between pages. Kept page 1 move.");
            }

            if (string.IsNullOrWhiteSpace(existingBlack) && !string.IsNullOrWhiteSpace(incomingBlack))
            {
                existing.BlackMove = CloneMove(incoming.BlackMove);
            }
            else if (!string.IsNullOrWhiteSpace(existingBlack) && !string.IsNullOrWhiteSpace(incomingBlack) &&
                     !string.Equals(existingBlack, incomingBlack, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Move {incoming.MoveNumber} black differs between pages. Kept page 1 move.");
            }

            merged[incoming.MoveNumber] = existing;
        }

        var mergedMoves = merged
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value)
            .ToList();

        return new MergeResult(
            mergedMoves,
            new ContinuationValidationResponse
            {
                IsValid = warnings.Count == 0,
                Page1EndMove = page1Range.EndMoveNumber,
                Page2StartMove = page2Range.StartMoveNumber,
                HasGap = hasGap,
                GapSize = gapSize,
                HasOverlap = hasOverlap,
                OverlapMoves = overlapMoves,
                Warnings = warnings
            });
    }

    private static string BuildPgnFromMovePairs(IEnumerable<ChessMovePair> moves, PgnMetadata? metadata, string result)
    {
        var normalizedResult = result is "1-0" or "0-1" or "1/2-1/2" ? result : "*";
        var sb = new StringBuilder();

        if (metadata?.GameDate.HasValue == true)
        {
            sb.AppendLine($"[Date \"{metadata.GameDate.Value:yyyy.MM.dd}\"]");
        }
        else
        {
            sb.AppendLine("[Date \"????.??.??\"]");
        }

        if (!string.IsNullOrWhiteSpace(metadata?.Round))
        {
            sb.AppendLine($"[Round \"{metadata.Round}\"]");
        }

        var whitePlayer = string.IsNullOrWhiteSpace(metadata?.WhitePlayer) ? "?" : metadata!.WhitePlayer;
        var blackPlayer = string.IsNullOrWhiteSpace(metadata?.BlackPlayer) ? "?" : metadata!.BlackPlayer;
        sb.AppendLine($"[White \"{whitePlayer}\"]");
        sb.AppendLine($"[Black \"{blackPlayer}\"]");
        sb.AppendLine($"[Result \"{normalizedResult}\"]");
        sb.AppendLine();

        foreach (var move in moves.OrderBy(m => m.MoveNumber))
        {
            var white = GetMoveNotation(move.WhiteMove);
            var black = GetMoveNotation(move.BlackMove);

            if (!string.IsNullOrWhiteSpace(white))
            {
                sb.Append($"{move.MoveNumber}. {white}");
                if (!string.IsNullOrWhiteSpace(black))
                {
                    sb.Append($" {black}");
                }
                sb.Append(' ');
                continue;
            }

            if (!string.IsNullOrWhiteSpace(black))
            {
                sb.Append($"{move.MoveNumber}... {black} ");
            }
        }

        sb.Append(normalizedResult);
        return sb.ToString().TrimEnd();
    }

    private static List<ChessMovePair> ParseMovePairsFromPgn(string? pgnContent)
    {
        if (string.IsNullOrWhiteSpace(pgnContent))
        {
            return new List<ChessMovePair>();
        }

        var cleaned = PgnHeaderRegex.Replace(pgnContent, string.Empty);
        cleaned = Regex.Replace(cleaned, @"\{[^}]*\}", " ");
        cleaned = Regex.Replace(cleaned, @"\([^)]*\)", " ");
        cleaned = cleaned
            .Replace("\r", " ")
            .Replace("\n", " ");

        var result = new SortedDictionary<int, ChessMovePair>();

        var pairPattern = new Regex(@"(?<num>\d+)\.(?!\.)\s*(?<white>[^\s]+)(?:\s+(?<black>[^\s]+))?", RegexOptions.Compiled);
        foreach (Match match in pairPattern.Matches(cleaned))
        {
            if (!int.TryParse(match.Groups["num"].Value, out var moveNumber))
            {
                continue;
            }

            if (!result.TryGetValue(moveNumber, out var movePair))
            {
                movePair = new ChessMovePair { MoveNumber = moveNumber };
            }

            var white = match.Groups["white"].Value.Trim();
            var black = match.Groups["black"].Success ? match.Groups["black"].Value.Trim() : string.Empty;

            if (!string.IsNullOrWhiteSpace(white) && white is not "1-0" and not "0-1" and not "1/2-1/2" and not "*")
            {
                movePair.WhiteMove = new Models.ValidatedMove
                {
                    Notation = white,
                    NormalizedNotation = white,
                    ValidationStatus = "valid",
                    ValidationText = string.Empty
                };
            }

            if (!string.IsNullOrWhiteSpace(black) && black is not "1-0" and not "0-1" and not "1/2-1/2" and not "*")
            {
                movePair.BlackMove = new Models.ValidatedMove
                {
                    Notation = black,
                    NormalizedNotation = black,
                    ValidationStatus = "valid",
                    ValidationText = string.Empty
                };
            }

            result[moveNumber] = movePair;
        }

        var blackOnlyPattern = new Regex(@"(?<num>\d+)\.\.\.\s*(?<black>[^\s]+)", RegexOptions.Compiled);
        foreach (Match match in blackOnlyPattern.Matches(cleaned))
        {
            if (!int.TryParse(match.Groups["num"].Value, out var moveNumber))
            {
                continue;
            }

            if (!result.TryGetValue(moveNumber, out var movePair))
            {
                movePair = new ChessMovePair { MoveNumber = moveNumber };
            }

            var black = match.Groups["black"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(black) && black is not "1-0" and not "0-1" and not "1/2-1/2" and not "*")
            {
                movePair.BlackMove = new Models.ValidatedMove
                {
                    Notation = black,
                    NormalizedNotation = black,
                    ValidationStatus = "valid",
                    ValidationText = string.Empty
                };
            }

            result[moveNumber] = movePair;
        }

        return result.Values
            .Where(m => !string.IsNullOrWhiteSpace(GetMoveNotation(m.WhiteMove)) || !string.IsNullOrWhiteSpace(GetMoveNotation(m.BlackMove)))
            .OrderBy(m => m.MoveNumber)
            .ToList();
    }

    private static string ExtractResultFromPgn(string pgnContent)
    {
        if (string.IsNullOrWhiteSpace(pgnContent))
        {
            return "*";
        }

        var resultTagMatch = Regex.Match(pgnContent, @"^\s*\[Result\s+""(?<result>.*?)""\]\s*$", RegexOptions.Multiline);
        if (resultTagMatch.Success)
        {
            return NormalizeResult(resultTagMatch.Groups["result"].Value);
        }

        var tailMatch = Regex.Match(pgnContent.Trim(), @"\s(1-0|0-1|1/2-1/2|\*)\s*$");
        return tailMatch.Success ? NormalizeResult(tailMatch.Groups[1].Value) : "*";
    }

    private static string NormalizeResult(string value)
    {
        return value switch
        {
            "1-0" => "1-0",
            "0-1" => "0-1",
            "1/2-1/2" => "1/2-1/2",
            _ => "*"
        };
    }

    private static string? GetMoveNotation(Models.ValidatedMove? move)
    {
        return string.IsNullOrWhiteSpace(move?.NormalizedNotation)
            ? move?.Notation
            : move.NormalizedNotation;
    }

    private static Models.ValidatedMove? CloneMove(Models.ValidatedMove? move)
    {
        if (move == null)
        {
            return null;
        }

        return new Models.ValidatedMove
        {
            Notation = move.Notation,
            NormalizedNotation = move.NormalizedNotation,
            ValidationStatus = move.ValidationStatus,
            ValidationText = move.ValidationText
        };
    }

    private static ChessMovePair CloneMovePair(ChessMovePair move)
    {
        return new ChessMovePair
        {
            MoveNumber = move.MoveNumber,
            WhiteMove = CloneMove(move.WhiteMove),
            BlackMove = CloneMove(move.BlackMove)
        };
    }

    private static GamePageInfoResponse ToPageInfo(GameImage image)
    {
        return new GamePageInfoResponse
        {
            ImageId = image.Id,
            PageNumber = image.PageNumber,
            StartingMoveNumber = image.StartingMoveNumber,
            EndingMoveNumber = image.EndingMoveNumber,
            UploadedAt = image.UploadedAt,
            Variant = string.IsNullOrWhiteSpace(image.Variant) ? "original" : image.Variant
        };
    }

    private sealed record StoredUploadImage(
        string FileName,
        string FilePath,
        string? CloudStorageUrl,
        string? CloudStorageObjectName,
        bool IsStoredInCloud);

    private sealed record PreparedImagePath(string PathToProcess, string? TempPathToDelete);

    private sealed record MoveRange(int StartMoveNumber, int EndMoveNumber);

    private sealed record MergeResult(List<ChessMovePair> Moves, ContinuationValidationResponse Validation);

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
