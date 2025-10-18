using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Repositories;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for managing chess games (CRUD operations)
/// </summary>
public class GameManagementService : IGameManagementService
{
    private readonly RepositoryFactory _repositoryFactory;
    private readonly ILogger<GameManagementService> _logger;

    public GameManagementService(
        RepositoryFactory repositoryFactory,
        ILogger<GameManagementService> logger)
    {
        _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
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
}

