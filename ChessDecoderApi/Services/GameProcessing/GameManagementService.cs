using ChessDecoderApi.DTOs;
using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Services;
using System.Text.RegularExpressions;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for managing chess games (CRUD operations)
/// </summary>
public class GameManagementService : IGameManagementService
{
    private readonly RepositoryFactory _repositoryFactory;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<GameManagementService> _logger;

    public GameManagementService(
        RepositoryFactory repositoryFactory,
        IImageProcessingService imageProcessingService,
        ILogger<GameManagementService> logger)
    {
        _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
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
                UploadedAt = img.UploadedAt
            }).ToList()
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
            var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
            var statsRepo = await _repositoryFactory.CreateGameStatisticsRepositoryAsync();

            // Delete related records (cascade should handle this, but being explicit)
            await imageRepo.DeleteByChessGameIdAsync(gameId);
            await statsRepo.DeleteByChessGameIdAsync(gameId);
            
            // Delete the game
            var result = await gameRepo.DeleteAsync(gameId);
            
            if (result)
            {
                _logger.LogInformation("Successfully deleted game {GameId}", gameId);
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

    private (List<string> whiteMoves, List<string> blackMoves) ExtractMovesFromPgn(string pgnContent)
    {
        var whiteMoves = new List<string>();
        var blackMoves = new List<string>();
        
        // Remove PGN headers and metadata
        var lines = pgnContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var movesSection = string.Join(" ", lines.Where(line => !line.StartsWith("[") && !string.IsNullOrWhiteSpace(line)));
        
        // Remove result markers and extra whitespace
        movesSection = movesSection.Replace("*", "").Replace("1-0", "").Replace("0-1", "").Replace("1/2-1/2", "").Trim();
        
        // Extract moves using regex pattern: "1. e4 e5 2. Nf3 Nc6" etc.
        var movePattern = @"\d+\.\s*([^\s]+)(?:\s+([^\s]+))?";
        var matches = Regex.Matches(movesSection, movePattern);
        
        foreach (Match match in matches)
        {
            // Add white move
            if (match.Groups[1].Success)
            {
                var whiteMove = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(whiteMove) && whiteMove != "*")
                {
                    whiteMoves.Add(whiteMove);
                }
            }
            
            // Add black move if present
            if (match.Groups[2].Success)
            {
                var blackMove = match.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(blackMove) && blackMove != "*")
                {
                    blackMoves.Add(blackMove);
                }
            }
        }
        
        return (whiteMoves, blackMoves);
    }
}

