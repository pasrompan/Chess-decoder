using ChessDecoderApi.DTOs.Responses;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for managing chess games (CRUD operations)
/// </summary>
public interface IGameManagementService
{
    /// <summary>
    /// Get a game by its ID
    /// </summary>
    Task<GameDetailsResponse?> GetGameByIdAsync(Guid gameId);

    /// <summary>
    /// Get all games for a specific user
    /// </summary>
    Task<GameListResponse> GetUserGamesAsync(string userId, int pageNumber = 1, int pageSize = 10);

    /// <summary>
    /// Delete a game
    /// </summary>
    Task<bool> DeleteGameAsync(Guid gameId);
}

