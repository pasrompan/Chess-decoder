using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services;

/// <summary>
/// Firestore database service - FREE database option!
/// Cost: $0/month for typical Chess Decoder traffic (1GB storage, 50K reads/day, 20K writes/day free)
/// </summary>
public interface IFirestoreService
{
    // User operations
    Task<User?> GetUserByIdAsync(string id);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task DeleteUserAsync(string id);
    
    // ChessGame operations
    Task<ChessGame?> GetChessGameByIdAsync(Guid id);
    Task<IEnumerable<ChessGame>> GetChessGamesByUserIdAsync(string userId, int limit = 100);
    Task<ChessGame> CreateChessGameAsync(ChessGame game);
    Task<ChessGame> UpdateChessGameAsync(ChessGame game);
    Task DeleteChessGameAsync(Guid id);
    
    // GameImage operations
    Task<GameImage?> GetGameImageByIdAsync(Guid id);
    Task<IEnumerable<GameImage>> GetGameImagesByGameIdAsync(Guid gameId);
    Task<GameImage> CreateGameImageAsync(GameImage image);
    Task DeleteGameImageAsync(Guid id);
    
    // GameStatistics operations
    Task<GameStatistics?> GetGameStatisticsByGameIdAsync(Guid gameId);
    Task<GameStatistics> CreateOrUpdateGameStatisticsAsync(GameStatistics statistics);
    Task DeleteGameStatisticsAsync(Guid id);
    
    // Utility
    Task<bool> IsAvailableAsync();
}

