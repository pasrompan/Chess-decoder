using ChessDecoderApi.Models;

namespace ChessDecoderApi.Repositories.Interfaces;

/// <summary>
/// Repository interface for ChessGame data access operations
/// </summary>
public interface IChessGameRepository
{
    /// <summary>
    /// Get a chess game by its unique ID
    /// </summary>
    Task<ChessGame?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get all chess games for a specific user
    /// </summary>
    Task<List<ChessGame>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Get paginated chess games for a specific user
    /// </summary>
    Task<(List<ChessGame> games, int totalCount)> GetByUserIdPaginatedAsync(
        string userId, 
        int pageNumber = 1, 
        int pageSize = 10);

    /// <summary>
    /// Create a new chess game
    /// </summary>
    Task<ChessGame> CreateAsync(ChessGame game);

    /// <summary>
    /// Update an existing chess game
    /// </summary>
    Task<ChessGame> UpdateAsync(ChessGame game);

    /// <summary>
    /// Delete a chess game by ID
    /// </summary>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Check if a chess game exists by ID
    /// </summary>
    Task<bool> ExistsAsync(Guid id);

    /// <summary>
    /// Get total count of games for a user
    /// </summary>
    Task<int> GetCountByUserIdAsync(string userId);
}

