using Google.Cloud.Firestore;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Firestore;

/// <summary>
/// Firestore implementation of IChessGameRepository
/// </summary>
public class FirestoreChessGameRepository : IChessGameRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirestoreChessGameRepository> _logger;
    private const string GAMES_COLLECTION = "chess_games";

    public FirestoreChessGameRepository(FirestoreDb firestoreDb, ILogger<FirestoreChessGameRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChessGame?> GetByIdAsync(Guid id)
    {
        var docRef = _firestoreDb.Collection(GAMES_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return null;
        
        var game = snapshot.ConvertTo<ChessGame>();
        game.Id = id;
        return game;
    }

    public async Task<List<ChessGame>> GetByUserIdAsync(string userId)
    {
        var query = _firestoreDb.Collection(GAMES_COLLECTION)
            .WhereEqualTo("UserId", userId)
            .OrderByDescending("ProcessedAt");
        
        var snapshot = await query.GetSnapshotAsync();
        
        return snapshot.Documents.Select(doc =>
        {
            var game = doc.ConvertTo<ChessGame>();
            game.Id = Guid.Parse(doc.Id);
            return game;
        }).ToList();
    }

    public async Task<(List<ChessGame> games, int totalCount)> GetByUserIdPaginatedAsync(
        string userId, 
        int pageNumber = 1, 
        int pageSize = 10)
    {
        // Get total count
        var totalQuery = _firestoreDb.Collection(GAMES_COLLECTION)
            .WhereEqualTo("UserId", userId);
        var totalSnapshot = await totalQuery.GetSnapshotAsync();
        var totalCount = totalSnapshot.Count;

        // Get paginated results
        var offset = (pageNumber - 1) * pageSize;
        var query = _firestoreDb.Collection(GAMES_COLLECTION)
            .WhereEqualTo("UserId", userId)
            .OrderByDescending("ProcessedAt")
            .Offset(offset)
            .Limit(pageSize);
        
        var snapshot = await query.GetSnapshotAsync();
        
        var games = snapshot.Documents.Select(doc =>
        {
            var game = doc.ConvertTo<ChessGame>();
            game.Id = Guid.Parse(doc.Id);
            return game;
        }).ToList();

        return (games, totalCount);
    }

    public async Task<ChessGame> CreateAsync(ChessGame game)
    {
        // Generate new Guid if not set
        if (game.Id == Guid.Empty)
        {
            game.Id = Guid.NewGuid();
        }
        
        game.ProcessedAt = DateTime.UtcNow;
        
        var docRef = _firestoreDb.Collection(GAMES_COLLECTION).Document(game.Id.ToString());
        await docRef.SetAsync(game);
        
        _logger.LogInformation("[Firestore] Created game: {GameId}", game.Id);
        return game;
    }

    public async Task<ChessGame> UpdateAsync(ChessGame game)
    {
        var docRef = _firestoreDb.Collection(GAMES_COLLECTION).Document(game.Id.ToString());
        await docRef.SetAsync(game, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Updated game: {GameId}", game.Id);
        return game;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GAMES_COLLECTION).Document(id.ToString());
            await docRef.DeleteAsync();
            
            _logger.LogInformation("[Firestore] Deleted game: {GameId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting game: {GameId}", id);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        var docRef = _firestoreDb.Collection(GAMES_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        return snapshot.Exists;
    }

    public async Task<int> GetCountByUserIdAsync(string userId)
    {
        var query = _firestoreDb.Collection(GAMES_COLLECTION)
            .WhereEqualTo("UserId", userId);
        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Count;
    }
}

