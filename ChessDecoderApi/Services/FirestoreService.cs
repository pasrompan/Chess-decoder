using Google.Cloud.Firestore;
using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services;

/// <summary>
/// Firestore database implementation - FREE for typical usage!
/// Free tier: 1GB storage, 50K reads/day, 20K writes/day
/// </summary>
public class FirestoreService : IFirestoreService
{
    private readonly FirestoreDb? _firestoreDb;
    private readonly ILogger<FirestoreService> _logger;
    private readonly bool _isAvailable;

    private const string USERS_COLLECTION = "users";
    private const string GAMES_COLLECTION = "chess_games";
    private const string IMAGES_COLLECTION = "game_images";
    private const string STATISTICS_COLLECTION = "game_statistics";
    private const string COUNTERS_COLLECTION = "counters";

    public FirestoreService(IConfiguration configuration, ILogger<FirestoreService> logger)
    {
        _logger = logger;

        try
        {
            var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") 
                ?? configuration["GoogleCloud:ProjectId"]
                ?? "chess-decoder-446310";

            _logger.LogInformation("[Firestore] Initializing with project: {ProjectId}", projectId);
            
            _firestoreDb = FirestoreDb.Create(projectId);
            _isAvailable = true;
            
            _logger.LogInformation("[Firestore] Successfully initialized - Cost: FREE (within free tier limits)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Firestore] Failed to initialize. Firestore not available.");
            _firestoreDb = null;
            _isAvailable = false;
        }
    }

    private void EnsureAvailable()
    {
        if (!_isAvailable || _firestoreDb == null)
        {
            throw new InvalidOperationException("Firestore is not available. Please enable Firestore API and create database.");
        }
    }

    public Task<bool> IsAvailableAsync() => Task.FromResult(_isAvailable);

    #region User Operations

    public async Task<User?> GetUserByIdAsync(string id)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(USERS_COLLECTION).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return null;
        
        var user = snapshot.ConvertTo<User>();
        user.Id = snapshot.Id;
        return user;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        EnsureAvailable();
        var query = _firestoreDb!.Collection(USERS_COLLECTION).WhereEqualTo("Email", email).Limit(1);
        var snapshot = await query.GetSnapshotAsync();
        
        var doc = snapshot.Documents.FirstOrDefault();
        if (doc == null) return null;
        
        var user = doc.ConvertTo<User>();
        user.Id = doc.Id;
        return user;
    }

    public async Task<User> CreateUserAsync(User user)
    {
        EnsureAvailable();
        
        user.CreatedAt = DateTime.UtcNow;
        user.LastLoginAt = DateTime.UtcNow;
        
        var docRef = _firestoreDb!.Collection(USERS_COLLECTION).Document(user.Id);
        await docRef.SetAsync(user);
        
        _logger.LogInformation("[Firestore] Created user: {UserId}", user.Id);
        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(USERS_COLLECTION).Document(user.Id);
        await docRef.SetAsync(user, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Updated user: {UserId}", user.Id);
        return user;
    }

    public async Task DeleteUserAsync(string id)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(USERS_COLLECTION).Document(id);
        await docRef.DeleteAsync();
        
        _logger.LogInformation("[Firestore] Deleted user: {UserId}", id);
    }

    #endregion

    #region ChessGame Operations

    public async Task<ChessGame?> GetChessGameByIdAsync(Guid id)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(GAMES_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return null;
        
        var game = snapshot.ConvertTo<ChessGame>();
        game.Id = id;
        return game;
    }

    public async Task<IEnumerable<ChessGame>> GetChessGamesByUserIdAsync(string userId, int limit = 100)
    {
        EnsureAvailable();
        var query = _firestoreDb!.Collection(GAMES_COLLECTION)
            .WhereEqualTo("UserId", userId)
            .OrderByDescending("ProcessedAt")
            .Limit(limit);
        
        var snapshot = await query.GetSnapshotAsync();
        
        return snapshot.Documents.Select(doc =>
        {
            var game = doc.ConvertTo<ChessGame>();
            game.Id = Guid.Parse(doc.Id);
            return game;
        }).ToList();
    }

    public async Task<ChessGame> CreateChessGameAsync(ChessGame game)
    {
        EnsureAvailable();
        
        // Generate new Guid if not set
        if (game.Id == Guid.Empty)
        {
            game.Id = Guid.NewGuid();
        }
        
        game.ProcessedAt = DateTime.UtcNow;
        
        var docRef = _firestoreDb!.Collection(GAMES_COLLECTION).Document(game.Id.ToString());
        await docRef.SetAsync(game);
        
        _logger.LogInformation("[Firestore] Created game: {GameId}", game.Id);
        return game;
    }

    public async Task<ChessGame> UpdateChessGameAsync(ChessGame game)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(GAMES_COLLECTION).Document(game.Id.ToString());
        await docRef.SetAsync(game, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Updated game: {GameId}", game.Id);
        return game;
    }

    public async Task DeleteChessGameAsync(Guid id)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(GAMES_COLLECTION).Document(id.ToString());
        await docRef.DeleteAsync();
        
        _logger.LogInformation("[Firestore] Deleted game: {GameId}", id);
    }

    #endregion

    #region GameImage Operations

    public async Task<GameImage?> GetGameImageByIdAsync(Guid id)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(IMAGES_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return null;
        
        var image = snapshot.ConvertTo<GameImage>();
        image.Id = id;
        
        // Manually parse chessGameId from string to Guid
        if (snapshot.TryGetValue<string>("chessGameId", out var chessGameIdStr) &&
            Guid.TryParse(chessGameIdStr, out var chessGameId))
        {
            image.ChessGameId = chessGameId;
        }
        
        return image;
    }

    public async Task<IEnumerable<GameImage>> GetGameImagesByGameIdAsync(Guid gameId)
    {
        EnsureAvailable();
        var query = _firestoreDb!.Collection(IMAGES_COLLECTION)
            .WhereEqualTo("chessGameId", gameId.ToString());
        
        var snapshot = await query.GetSnapshotAsync();
        
        return snapshot.Documents.Select(doc =>
        {
            var image = doc.ConvertTo<GameImage>();
            image.Id = Guid.Parse(doc.Id);
            
            // Manually parse chessGameId from string to Guid
            if (doc.TryGetValue<string>("chessGameId", out var chessGameIdStr) &&
                Guid.TryParse(chessGameIdStr, out var chessGameId))
            {
                image.ChessGameId = chessGameId;
            }
            
            return image;
        }).ToList();
    }

    public async Task<GameImage> CreateGameImageAsync(GameImage image)
    {
        EnsureAvailable();
        
        // Generate new Guid if not set
        if (image.Id == Guid.Empty)
        {
            image.Id = Guid.NewGuid();
        }
        
        image.UploadedAt = DateTime.UtcNow;
        
        // Create a dictionary for Firestore, manually converting Guid fields to strings
        var firestoreData = new Dictionary<string, object?>
        {
            { "chessGameId", image.ChessGameId.ToString() }, // Convert Guid to string
            { "FileName", image.FileName },
            { "FilePath", image.FilePath },
            { "FileType", image.FileType ?? "" },
            { "FileSizeBytes", image.FileSizeBytes },
            { "UploadedAt", image.UploadedAt },
            { "CloudStorageUrl", image.CloudStorageUrl ?? "" },
            { "CloudStorageObjectName", image.CloudStorageObjectName ?? "" },
            { "IsStoredInCloud", image.IsStoredInCloud }
        };
        
        var docRef = _firestoreDb!.Collection(IMAGES_COLLECTION).Document(image.Id.ToString());
        await docRef.SetAsync(firestoreData);
        
        _logger.LogInformation("[Firestore] Created image: {ImageId}", image.Id);
        return image;
    }

    public async Task DeleteGameImageAsync(Guid id)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(IMAGES_COLLECTION).Document(id.ToString());
        await docRef.DeleteAsync();
        
        _logger.LogInformation("[Firestore] Deleted image: {ImageId}", id);
    }

    #endregion

    #region GameStatistics Operations

    public async Task<GameStatistics?> GetGameStatisticsByGameIdAsync(Guid gameId)
    {
        EnsureAvailable();
        var query = _firestoreDb!.Collection(STATISTICS_COLLECTION)
            .WhereEqualTo("chessGameId", gameId.ToString())
            .Limit(1);
        
        var snapshot = await query.GetSnapshotAsync();
        var doc = snapshot.Documents.FirstOrDefault();
        
        if (doc == null) return null;
        
        var stats = doc.ConvertTo<GameStatistics>();
        stats.Id = Guid.Parse(doc.Id);
        
        // Manually parse chessGameId from string to Guid
        if (doc.TryGetValue<string>("chessGameId", out var chessGameIdStr) &&
            Guid.TryParse(chessGameIdStr, out var chessGameId))
        {
            stats.ChessGameId = chessGameId;
        }
        
        return stats;
    }

    public async Task<GameStatistics> CreateOrUpdateGameStatisticsAsync(GameStatistics statistics)
    {
        EnsureAvailable();
        
        // Generate new Guid if not set
        if (statistics.Id == Guid.Empty)
        {
            statistics.Id = Guid.NewGuid();
        }
        
        statistics.CreatedAt = DateTime.UtcNow;
        
        // Create a dictionary for Firestore, manually converting Guid fields to strings
        var firestoreData = new Dictionary<string, object?>
        {
            { "chessGameId", statistics.ChessGameId.ToString() }, // Convert Guid to string
            { "TotalMoves", statistics.TotalMoves },
            { "ValidMoves", statistics.ValidMoves },
            { "InvalidMoves", statistics.InvalidMoves },
            { "Opening", statistics.Opening ?? "" },
            { "Result", statistics.Result ?? "" },
            { "CreatedAt", statistics.CreatedAt }
        };
        
        var docRef = _firestoreDb!.Collection(STATISTICS_COLLECTION).Document(statistics.Id.ToString());
        await docRef.SetAsync(firestoreData, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Saved statistics: {StatsId}", statistics.Id);
        return statistics;
    }

    public async Task DeleteGameStatisticsAsync(Guid id)
    {
        EnsureAvailable();
        var docRef = _firestoreDb!.Collection(STATISTICS_COLLECTION).Document(id.ToString());
        await docRef.DeleteAsync();
        
        _logger.LogInformation("[Firestore] Deleted statistics: {StatsId}", id);
    }

    #endregion
}

