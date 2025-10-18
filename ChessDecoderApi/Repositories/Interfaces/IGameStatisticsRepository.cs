using ChessDecoderApi.Models;

namespace ChessDecoderApi.Repositories.Interfaces;

/// <summary>
/// Repository interface for GameStatistics data access operations
/// </summary>
public interface IGameStatisticsRepository
{
    /// <summary>
    /// Get game statistics by its unique ID
    /// </summary>
    Task<GameStatistics?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get statistics for a specific chess game
    /// </summary>
    Task<GameStatistics?> GetByChessGameIdAsync(Guid chessGameId);

    /// <summary>
    /// Create new game statistics
    /// </summary>
    Task<GameStatistics> CreateAsync(GameStatistics statistics);

    /// <summary>
    /// Update existing game statistics
    /// </summary>
    Task<GameStatistics> UpdateAsync(GameStatistics statistics);

    /// <summary>
    /// Create or update game statistics (upsert operation)
    /// </summary>
    Task<GameStatistics> CreateOrUpdateAsync(GameStatistics statistics);

    /// <summary>
    /// Delete game statistics by ID
    /// </summary>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Delete statistics for a specific chess game
    /// </summary>
    Task<bool> DeleteByChessGameIdAsync(Guid chessGameId);

    /// <summary>
    /// Check if game statistics exist by ID
    /// </summary>
    Task<bool> ExistsAsync(Guid id);
}

