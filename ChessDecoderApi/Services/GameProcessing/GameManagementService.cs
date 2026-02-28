using ChessDecoderApi.DTOs;
using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for managing chess games (CRUD operations)
/// </summary>
public class GameManagementService : IGameManagementService
{
    private readonly RepositoryFactory _repositoryFactory;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IProjectService _projectService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<GameManagementService> _logger;

    public GameManagementService(
        RepositoryFactory repositoryFactory,
        IImageProcessingService imageProcessingService,
        IProjectService projectService,
        ICloudStorageService cloudStorageService,
        ILogger<GameManagementService> logger)
    {
        _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _cloudStorageService = cloudStorageService ?? throw new ArgumentNullException(nameof(cloudStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameDetailsResponse?> GetGameByIdAsync(Guid gameId)
    {
        var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
        var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
        var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

        var game = await gameRepo.GetByIdAsync(gameId);
        if (game == null)
        {
            return null;
        }

        var images = await imageRepo.GetByChessGameIdAsync(gameId);
        var statistics = await statsRepo.GetByChessGameIdAsync(gameId);

        return MapToGameDetailsResponse(game, images, statistics);
    }

    private GameDetailsResponse MapToGameDetailsResponse(
        Models.ChessGame game, 
        IEnumerable<Models.GameImage> images, 
        Models.GameStatistics? statistics)
    {
        return new GameDetailsResponse
        {
            GameId = game.Id,
            UserId = game.UserId,
            Title = game.Title,
            Description = game.Description,
            PgnContent = game.PgnContent,
            ProcessedAt = game.ProcessedAt,
            ProcessingTimeMs = game.ProcessingTimeMs,
            IsValid = game.IsValid,
            ValidationMessage = game.ValidationMessage,
            WhitePlayer = game.WhitePlayer,
            BlackPlayer = game.BlackPlayer,
            GameDate = game.GameDate,
            Round = game.Round,
            Result = game.Result,
            HasContinuation = game.HasContinuation,
            ProcessingCompleted = game.ProcessingCompleted,
            LastEditedAt = game.LastEditedAt,
            EditCount = game.EditCount,
            Statistics = statistics != null ? new GameStatisticsDto
            {
                TotalMoves = statistics.TotalMoves,
                ValidMoves = statistics.ValidMoves,
                InvalidMoves = statistics.InvalidMoves,
                Opening = statistics.Opening,
                Result = statistics.Result
            } : null,
            Images = images.Select(img => new GameImageDto
            {
                ImageId = img.Id,
                FileName = img.FileName,
                CloudStorageUrl = img.CloudStorageUrl,
                IsStoredInCloud = img.IsStoredInCloud,
                UploadedAt = img.UploadedAt,
                Variant = string.IsNullOrWhiteSpace(img.Variant) ? "original" : img.Variant,
                PageNumber = img.PageNumber <= 0 ? 1 : img.PageNumber,
                StartingMoveNumber = img.StartingMoveNumber,
                EndingMoveNumber = img.EndingMoveNumber,
                ContinuationImageId = img.ContinuationImageId
            })
            .OrderBy(img => img.PageNumber)
            .ThenBy(img => img.Variant, StringComparer.OrdinalIgnoreCase)
            .ToList()
        };
    }

    public async Task<GameListResponse> GetUserGamesAsync(string userId, int pageNumber = 1, int pageSize = 10)
    {
        var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
        var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

        var (games, totalCount) = await gameRepo.GetByUserIdPaginatedAsync(userId, pageNumber, pageSize);

        var gameSummaries = new List<GameSummaryDto>();
        
        foreach (var game in games)
        {
            var statistics = await statsRepo.GetByChessGameIdAsync(game.Id);
            
            gameSummaries.Add(new GameSummaryDto
            {
                GameId = game.Id,
                Title = game.Title,
                ProcessedAt = game.ProcessedAt,
                IsValid = game.IsValid,
                HasContinuation = game.HasContinuation,
                TotalMoves = statistics?.TotalMoves ?? 0,
                Opening = statistics?.Opening
            });
        }

        return new GameListResponse
        {
            Games = gameSummaries,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<bool> DeleteGameAsync(Guid gameId)
    {
        try
        {
            var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
            // Soft-delete the game; related records remain for audit/recovery.
            var result = await gameRepo.DeleteAsync(gameId);
            
            if (result)
            {
                _logger.LogInformation("Successfully soft-deleted game {GameId}", gameId);
            }
            else
            {
                _logger.LogWarning("Game {GameId} not found for deletion", gameId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting game {GameId}", gameId);
            return false;
        }
    }

    public async Task<bool> UpdateGameMetadataAsync(Guid gameId, UpdateGameMetadataRequest request)
    {
        try
        {
            var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
            var game = await gameRepo.GetByIdAsync(gameId);
            
            if (game == null)
            {
                _logger.LogWarning("Game {GameId} not found for metadata update", gameId);
                return false;
            }

            // Update metadata fields (only if provided)
            if (request.WhitePlayer != null)
            {
                game.WhitePlayer = string.IsNullOrWhiteSpace(request.WhitePlayer) ? null : request.WhitePlayer;
            }
            if (request.BlackPlayer != null)
            {
                game.BlackPlayer = string.IsNullOrWhiteSpace(request.BlackPlayer) ? null : request.BlackPlayer;
            }
            if (request.GameDate.HasValue)
            {
                game.GameDate = request.GameDate;
            }
            if (request.Round != null)
            {
                game.Round = string.IsNullOrWhiteSpace(request.Round) ? null : request.Round;
            }
            if (request.Result != null)
            {
                game.Result = string.IsNullOrWhiteSpace(request.Result) ? null : request.Result;
            }

            // Extract moves from existing PGN and regenerate with new metadata
            var (whiteMoves, blackMoves) = ExtractMovesFromPgn(game.PgnContent);
            
            var pgnMetadata = new PgnMetadata
            {
                WhitePlayer = game.WhitePlayer,
                BlackPlayer = game.BlackPlayer,
                GameDate = game.GameDate,
                Round = game.Round
            };

            // Regenerate PGN with new metadata
            game.PgnContent = _imageProcessingService.GeneratePGNContentAsync(whiteMoves, blackMoves, pgnMetadata);

            // Save updated game
            await gameRepo.UpdateAsync(game);
            
            _logger.LogInformation("Successfully updated metadata for game {GameId}", gameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metadata for game {GameId}", gameId);
            return false;
        }
    }

    public async Task<GameDetailsResponse?> UpdatePgnContentAsync(Guid gameId, string userId, string pgnContent)
    {
        try
        {
            var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
            var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
            var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

            var game = await gameRepo.GetByIdAsync(gameId);
            
            if (game == null)
            {
                _logger.LogWarning("Game {GameId} not found for PGN update", gameId);
                return null;
            }

            // Verify the user owns this game
            if (game.UserId != userId)
            {
                _logger.LogWarning("User {UserId} does not own game {GameId}", userId, gameId);
                return null;
            }

            // Validate PGN content is not empty
            if (string.IsNullOrWhiteSpace(pgnContent))
            {
                _logger.LogWarning("Empty PGN content provided for game {GameId}", gameId);
                throw new ArgumentException("PGN content cannot be empty");
            }

            string normalizedPgnContent;
            try
            {
                normalizedPgnContent = NormalizePgnForPersistence(pgnContent, game);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid PGN content provided for game {GameId}", gameId);
                throw;
            }

            // Update the game
            game.PgnContent = normalizedPgnContent;
            game.LastEditedAt = DateTime.UtcNow;
            game.EditCount++;

            await gameRepo.UpdateAsync(game);
            
            _logger.LogInformation("Successfully updated PGN for game {GameId}, edit count: {EditCount}", gameId, game.EditCount);

            // Add history entry for the PGN update
            try
            {
                await _projectService.AddHistoryEntryAsync(
                    gameId, 
                    "modification", 
                    $"PGN content updated (edit #{game.EditCount})",
                    new Dictionary<string, object> { { "editCount", game.EditCount } });
            }
            catch (Exception historyEx)
            {
                _logger.LogWarning(historyEx, "Failed to add history entry for PGN update, continuing...");
            }

            // Keep project processing snapshot aligned with the latest edited PGN
            try
            {
                var existingProject = await _projectService.GetProjectByGameIdAsync(gameId);
                var existingProcessing = existingProject?.Processing;

                var processingData = new ProcessingData
                {
                    ProcessedAt = existingProcessing?.ProcessedAt ?? game.ProcessedAt,
                    PgnContent = normalizedPgnContent,
                    ValidationStatus = existingProcessing?.ValidationStatus ?? (game.IsValid ? "valid" : "invalid"),
                    ProcessingTimeMs = existingProcessing?.ProcessingTimeMs ?? game.ProcessingTimeMs
                };

                await _projectService.UpdateProcessingDataAsync(gameId, processingData);
            }
            catch (Exception processingHistoryEx)
            {
                _logger.LogWarning(processingHistoryEx, "Failed to update project processing data for game {GameId}, continuing...", gameId);
            }

            // Return updated game details
            var images = await imageRepo.GetByChessGameIdAsync(gameId);
            var statistics = await statsRepo.GetByChessGameIdAsync(gameId);
            return MapToGameDetailsResponse(game, images, statistics);
        }
        catch (ArgumentException)
        {
            throw; // Re-throw validation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating PGN for game {GameId}", gameId);
            throw;
        }
    }

    public async Task<GameDetailsResponse?> MarkProcessingCompleteAsync(Guid gameId, string userId)
    {
        try
        {
            var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
            var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
            var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

            var game = await gameRepo.GetByIdAsync(gameId);
            
            if (game == null)
            {
                _logger.LogWarning("Game {GameId} not found for completion marking", gameId);
                return null;
            }

            // Verify the user owns this game
            if (game.UserId != userId)
            {
                _logger.LogWarning("User {UserId} does not own game {GameId}", userId, gameId);
                return null;
            }

            // Mark as completed (idempotent - can be called multiple times)
            if (!game.ProcessingCompleted)
            {
                game.ProcessingCompleted = true;
                await gameRepo.UpdateAsync(game);
                _logger.LogInformation("Game {GameId} marked as processing completed", gameId);

                // Add history entry for export/completion
                try
                {
                    await _projectService.AddHistoryEntryAsync(
                        gameId, 
                        "export", 
                        "Game exported to Lichess/Chess.com");
                }
                catch (Exception historyEx)
                {
                    _logger.LogWarning(historyEx, "Failed to add history entry for completion, continuing...");
                }
            }
            else
            {
                _logger.LogDebug("Game {GameId} was already marked as completed", gameId);
            }

            // Return updated game details
            var images = await imageRepo.GetByChessGameIdAsync(gameId);
            var statistics = await statsRepo.GetByChessGameIdAsync(gameId);
            return MapToGameDetailsResponse(game, images, statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking game {GameId} as completed", gameId);
            throw;
        }
    }

    public async Task<GameImageContentResult?> GetGameImageAsync(Guid gameId, string userId, string variant = "processed", int? pageNumber = null)
    {
        try
        {
            var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
            var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();

            var game = await gameRepo.GetByIdAsync(gameId);
            if (game == null)
            {
                return null;
            }

            if (game.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to access image for game {GameId} without ownership", userId, gameId);
                return null;
            }

            var images = await imageRepo.GetByChessGameIdAsync(gameId);
            if (!images.Any())
            {
                return null;
            }

            if (pageNumber.HasValue)
            {
                images = images
                    .Where(i => i.PageNumber == pageNumber.Value)
                    .ToList();

                if (!images.Any())
                {
                    _logger.LogWarning("No images found for game {GameId} and page {PageNumber}", gameId, pageNumber.Value);
                    return null;
                }
            }

            var requestedVariant = string.IsNullOrWhiteSpace(variant) ? "processed" : variant.Trim().ToLowerInvariant();
            var selectedImage = images.FirstOrDefault(i =>
                string.Equals(i.Variant, requestedVariant, StringComparison.OrdinalIgnoreCase))
                ?? images.FirstOrDefault(i => string.Equals(i.Variant, "original", StringComparison.OrdinalIgnoreCase))
                ?? images.OrderBy(i => i.PageNumber).First();

            if (selectedImage.IsStoredInCloud)
            {
                var objectName = selectedImage.CloudStorageObjectName;
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    _logger.LogWarning("Cloud image for game {GameId} is missing object name", gameId);
                    return null;
                }

                var stream = await _cloudStorageService.DownloadGameImageAsync(objectName);
                return new GameImageContentResult
                {
                    Stream = stream,
                    ContentType = string.IsNullOrWhiteSpace(selectedImage.FileType) ? "image/png" : selectedImage.FileType,
                    Variant = string.IsNullOrWhiteSpace(selectedImage.Variant) ? "original" : selectedImage.Variant
                };
            }

            if (string.IsNullOrWhiteSpace(selectedImage.FilePath) || !File.Exists(selectedImage.FilePath))
            {
                _logger.LogWarning("Local image file not found for game {GameId}, path: {Path}", gameId, selectedImage.FilePath);
                return null;
            }

            var localStream = new FileStream(selectedImage.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new GameImageContentResult
            {
                Stream = localStream,
                ContentType = string.IsNullOrWhiteSpace(selectedImage.FileType) ? "image/png" : selectedImage.FileType,
                Variant = string.IsNullOrWhiteSpace(selectedImage.Variant) ? "original" : selectedImage.Variant
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image for game {GameId}", gameId);
            return null;
        }
    }

    private (List<string> whiteMoves, List<string> blackMoves) ExtractMovesFromPgn(string pgnContent)
    {
        var whiteMoves = new List<string>();
        var blackMoves = new List<string>();

        if (string.IsNullOrWhiteSpace(pgnContent))
        {
            return (whiteMoves, blackMoves);
        }

        // Normalize line endings and remove PGN headers (including indented headers).
        var normalized = pgnContent.Replace("\r\n", "\n");
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var movesSection = string.Join(" ", lines
            .Where(line => !line.TrimStart().StartsWith("[", StringComparison.Ordinal))
            .Where(line => !string.IsNullOrWhiteSpace(line)));

        // Remove PGN comments/variations and game result markers.
        movesSection = Regex.Replace(movesSection, @"\{[^}]*\}", " ");
        movesSection = Regex.Replace(movesSection, @"\([^)]*\)", " ");
        movesSection = Regex.Replace(movesSection, @"\b(?:1-0|0-1|1/2-1/2|\*)\b", " ");
        movesSection = Regex.Replace(movesSection, @"\s+", " ").Trim();

        // Extract moves like: "1. e4 e5", "1... e5", etc.
        var movePattern = @"\d+\.(?:\.\.)?\s*([^\s]+)(?:\s+([^\s]+))?";
        var matches = Regex.Matches(movesSection, movePattern);

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                var whiteMove = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(whiteMove) && whiteMove != "*")
                {
                    whiteMoves.Add(whiteMove);
                }
            }

            if (match.Groups[2].Success)
            {
                var blackMove = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(blackMove) && blackMove != "*")
                {
                    blackMoves.Add(blackMove);
                }
            }
        }

        return (whiteMoves, blackMoves);
    }

    private string NormalizePgnForPersistence(string pgnContent, Models.ChessGame game)
    {
        var normalized = pgnContent.Replace("\r\n", "\n").Trim();
        if (!LooksCorruptedPgn(normalized))
        {
            return normalized;
        }

        var (whiteMoves, blackMoves) = ExtractMovesFromPgn(normalized);
        if (whiteMoves.Count == 0 && blackMoves.Count == 0)
        {
            throw new ArgumentException("PGN content does not contain valid move data");
        }

        var white = GetHeaderValue(normalized, "White") ?? game.WhitePlayer;
        var black = GetHeaderValue(normalized, "Black") ?? game.BlackPlayer;
        var round = GetHeaderValue(normalized, "Round") ?? game.Round;
        var date = ParsePgnDate(GetHeaderValue(normalized, "Date")) ?? game.GameDate;

        var metadata = new PgnMetadata
        {
            WhitePlayer = white,
            BlackPlayer = black,
            Round = round,
            GameDate = date
        };

        var rebuilt = _imageProcessingService.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata).TrimEnd();

        var eventHeader = GetHeaderValue(normalized, "Event");
        var siteHeader = GetHeaderValue(normalized, "Site");
        if (!string.IsNullOrWhiteSpace(eventHeader) || !string.IsNullOrWhiteSpace(siteHeader))
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(eventHeader))
            {
                sb.AppendLine($"[Event \"{eventHeader}\"]");
            }

            if (!string.IsNullOrWhiteSpace(siteHeader))
            {
                sb.AppendLine($"[Site \"{siteHeader}\"]");
            }

            sb.AppendLine(rebuilt);
            rebuilt = sb.ToString().TrimEnd();
        }

        var resultValue = NormalizeResultValue(GetHeaderValue(normalized, "Result"));
        if (resultValue != "*")
        {
            rebuilt = rebuilt.Replace("[Result \"*\"]", $"[Result \"{resultValue}\"]", StringComparison.Ordinal);
            rebuilt = Regex.Replace(rebuilt, @"\s\*$", $" {resultValue}");
        }

        _logger.LogWarning("Normalized corrupted PGN payload before persistence for game {GameId}", game.Id);
        return rebuilt;
    }

    private static bool LooksCorruptedPgn(string pgnContent)
    {
        var lines = pgnContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var moveLines = lines
            .Where(line => !line.TrimStart().StartsWith("[", StringComparison.Ordinal))
            .ToList();
        var moveSection = string.Join(" ", moveLines);

        // Header tags should never appear in the move section.
        return moveSection.Contains('[', StringComparison.Ordinal) || moveSection.Contains(']', StringComparison.Ordinal);
    }

    private static string? GetHeaderValue(string pgnContent, string tag)
    {
        var pattern = $@"^\s*\[{Regex.Escape(tag)}\s+""(?<value>.*?)""\]\s*$";
        var match = Regex.Match(pgnContent, pattern, RegexOptions.Multiline);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static DateTime? ParsePgnDate(string? dateValue)
    {
        if (string.IsNullOrWhiteSpace(dateValue) || dateValue.Contains('?'))
        {
            return null;
        }

        if (DateTime.TryParseExact(dateValue, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return null;
    }

    private static string NormalizeResultValue(string? result)
    {
        return result switch
        {
            "1-0" => "1-0",
            "0-1" => "0-1",
            "1/2-1/2" => "1/2-1/2",
            _ => "*"
        };
    }
}
