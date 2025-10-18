using ChessDecoderApi.Models;

namespace ChessDecoderApi.Repositories.Interfaces;

/// <summary>
/// Repository interface for GameImage data access operations
/// </summary>
public interface IGameImageRepository
{
    /// <summary>
    /// Get a game image by its unique ID
    /// </summary>
    Task<GameImage?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get all images for a specific chess game
    /// </summary>
    Task<List<GameImage>> GetByChessGameIdAsync(Guid chessGameId);

    /// <summary>
    /// Create a new game image
    /// </summary>
    Task<GameImage> CreateAsync(GameImage image);

    /// <summary>
    /// Update an existing game image
    /// </summary>
    Task<GameImage> UpdateAsync(GameImage image);

    /// <summary>
    /// Delete a game image by ID
    /// </summary>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Delete all images for a specific chess game
    /// </summary>
    Task<bool> DeleteByChessGameIdAsync(Guid chessGameId);

    /// <summary>
    /// Check if a game image exists by ID
    /// </summary>
    Task<bool> ExistsAsync(Guid id);
}

