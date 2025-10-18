using Google.Cloud.Firestore;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Firestore;

/// <summary>
/// Firestore implementation of IGameStatisticsRepository
/// </summary>
public class FirestoreGameStatisticsRepository : IGameStatisticsRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirestoreGameStatisticsRepository> _logger;
    private const string STATISTICS_COLLECTION = "game_statistics";

    public FirestoreGameStatisticsRepository(FirestoreDb firestoreDb, ILogger<FirestoreGameStatisticsRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameStatistics?> GetByIdAsync(Guid id)
    {
        var docRef = _firestoreDb.Collection(STATISTICS_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return null;
        
        var stats = snapshot.ConvertTo<GameStatistics>();
        stats.Id = id;
        
        // Manually parse chessGameId from string to Guid
        if (snapshot.TryGetValue<string>("chessGameId", out var chessGameIdStr) &&
            Guid.TryParse(chessGameIdStr, out var chessGameId))
        {
            stats.ChessGameId = chessGameId;
        }
        
        return stats;
    }

    public async Task<GameStatistics?> GetByChessGameIdAsync(Guid chessGameId)
    {
        var query = _firestoreDb.Collection(STATISTICS_COLLECTION)
            .WhereEqualTo("chessGameId", chessGameId.ToString())
            .Limit(1);
        
        var snapshot = await query.GetSnapshotAsync();
        var doc = snapshot.Documents.FirstOrDefault();
        
        if (doc == null) return null;
        
        var stats = doc.ConvertTo<GameStatistics>();
        stats.Id = Guid.Parse(doc.Id);
        
        // Manually parse chessGameId from string to Guid
        if (doc.TryGetValue<string>("chessGameId", out var chessGameIdStr) &&
            Guid.TryParse(chessGameIdStr, out var parsedGameId))
        {
            stats.ChessGameId = parsedGameId;
        }
        
        return stats;
    }

    public async Task<GameStatistics> CreateAsync(GameStatistics statistics)
    {
        // Generate new Guid if not set
        if (statistics.Id == Guid.Empty)
        {
            statistics.Id = Guid.NewGuid();
        }
        
        statistics.CreatedAt = DateTime.UtcNow;
        
        // Create a dictionary for Firestore, manually converting Guid fields to strings
        var firestoreData = new Dictionary<string, object?>
        {
            { "chessGameId", statistics.ChessGameId.ToString() },
            { "TotalMoves", statistics.TotalMoves },
            { "ValidMoves", statistics.ValidMoves },
            { "InvalidMoves", statistics.InvalidMoves },
            { "Opening", statistics.Opening ?? "" },
            { "Result", statistics.Result ?? "" },
            { "CreatedAt", statistics.CreatedAt }
        };
        
        var docRef = _firestoreDb.Collection(STATISTICS_COLLECTION).Document(statistics.Id.ToString());
        await docRef.SetAsync(firestoreData);
        
        _logger.LogInformation("[Firestore] Created statistics: {StatsId}", statistics.Id);
        return statistics;
    }

    public async Task<GameStatistics> UpdateAsync(GameStatistics statistics)
    {
        var firestoreData = new Dictionary<string, object?>
        {
            { "chessGameId", statistics.ChessGameId.ToString() },
            { "TotalMoves", statistics.TotalMoves },
            { "ValidMoves", statistics.ValidMoves },
            { "InvalidMoves", statistics.InvalidMoves },
            { "Opening", statistics.Opening ?? "" },
            { "Result", statistics.Result ?? "" },
            { "CreatedAt", statistics.CreatedAt }
        };
        
        var docRef = _firestoreDb.Collection(STATISTICS_COLLECTION).Document(statistics.Id.ToString());
        await docRef.SetAsync(firestoreData, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Updated statistics: {StatsId}", statistics.Id);
        return statistics;
    }

    public async Task<GameStatistics> CreateOrUpdateAsync(GameStatistics statistics)
    {
        // Check if statistics already exist for this game
        var existing = await GetByChessGameIdAsync(statistics.ChessGameId);
        
        if (existing != null)
        {
            statistics.Id = existing.Id;
            return await UpdateAsync(statistics);
        }
        else
        {
            return await CreateAsync(statistics);
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var docRef = _firestoreDb.Collection(STATISTICS_COLLECTION).Document(id.ToString());
            await docRef.DeleteAsync();
            
            _logger.LogInformation("[Firestore] Deleted statistics: {StatsId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting statistics: {StatsId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteByChessGameIdAsync(Guid chessGameId)
    {
        try
        {
            var stats = await GetByChessGameIdAsync(chessGameId);
            if (stats != null)
            {
                await DeleteAsync(stats.Id);
            }
            
            _logger.LogInformation("[Firestore] Deleted statistics for game: {GameId}", chessGameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting statistics for game: {GameId}", chessGameId);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        var docRef = _firestoreDb.Collection(STATISTICS_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        return snapshot.Exists;
    }
}

