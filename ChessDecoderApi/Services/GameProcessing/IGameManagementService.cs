using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;

namespace ChessDecoderApi.Services.GameProcessing;

public class GameImageContentResult
{
    public required Stream Stream { get; set; }
    public required string ContentType { get; set; }
    public required string Variant { get; set; }
}

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

    /// <summary>
    /// Update game metadata (player details, date, round)
    /// </summary>
    Task<bool> UpdateGameMetadataAsync(Guid gameId, UpdateGameMetadataRequest request);

    /// <summary>
    /// Update PGN content for a game
    /// </summary>
    Task<GameDetailsResponse?> UpdatePgnContentAsync(Guid gameId, string userId, string pgnContent);

    /// <summary>
    /// Mark a game's processing as completed (user exported to Lichess/Chess.com)
    /// </summary>
    Task<GameDetailsResponse?> MarkProcessingCompleteAsync(Guid gameId, string userId);

    /// <summary>
    /// Get an image for a game if owned by the user.
    /// Falls back to original image if requested variant is unavailable.
    /// </summary>
    Task<GameImageContentResult?> GetGameImageAsync(Guid gameId, string userId, string variant = "processed", int? pageNumber = null);
}
