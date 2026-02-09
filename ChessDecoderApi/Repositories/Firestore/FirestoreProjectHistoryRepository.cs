using Google.Cloud.Firestore;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Firestore;

/// <summary>
/// Firestore implementation of IProjectHistoryRepository
/// </summary>
public class FirestoreProjectHistoryRepository : IProjectHistoryRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirestoreProjectHistoryRepository> _logger;
    private const string HISTORY_COLLECTION = "projectHistories";

    public FirestoreProjectHistoryRepository(FirestoreDb firestoreDb, ILogger<FirestoreProjectHistoryRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProjectHistory?> GetByIdAsync(Guid id)
    {
        var docRef = _firestoreDb.Collection(HISTORY_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return null;
        
        var history = snapshot.ConvertTo<ProjectHistory>();
        history.Id = Guid.Parse(snapshot.Id);
        return history;
    }

    public async Task<ProjectHistory?> GetByGameIdAsync(Guid gameId)
    {
        var query = _firestoreDb.Collection(HISTORY_COLLECTION)
            .WhereEqualTo("GameId", gameId.ToString())
            .Limit(1);
        var snapshot = await query.GetSnapshotAsync();
        
        var doc = snapshot.Documents.FirstOrDefault();
        if (doc == null) return null;
        
        var history = doc.ConvertTo<ProjectHistory>();
        history.Id = Guid.Parse(doc.Id);
        return history;
    }

    public async Task<List<ProjectHistory>> GetByUserIdAsync(string userId)
    {
        var query = _firestoreDb.Collection(HISTORY_COLLECTION)
            .WhereEqualTo("UserId", userId)
            .OrderByDescending("CreatedAt");
        var snapshot = await query.GetSnapshotAsync();
        
        var histories = new List<ProjectHistory>();
        foreach (var doc in snapshot.Documents)
        {
            var history = doc.ConvertTo<ProjectHistory>();
            history.Id = Guid.Parse(doc.Id);
            histories.Add(history);
        }
        
        return histories;
    }

    public async Task<ProjectHistory> CreateAsync(ProjectHistory history)
    {
        if (history.Id == Guid.Empty)
        {
            history.Id = Guid.NewGuid();
        }
        
        history.CreatedAt = DateTime.UtcNow;
        
        // Convert to Firestore-compatible format
        var firestoreData = new Dictionary<string, object?>
        {
            { "GameId", history.GameId.ToString() },
            { "UserId", history.UserId },
            { "CreatedAt", history.CreatedAt },
            { "InitialUpload", ConvertToFirestore(history.InitialUpload) },
            { "Processing", ConvertToFirestore(history.Processing) },
            { "Versions", history.Versions.Select(ConvertToFirestore).ToList() }
        };
        
        var docRef = _firestoreDb.Collection(HISTORY_COLLECTION).Document(history.Id.ToString());
        await docRef.SetAsync(firestoreData);
        
        _logger.LogInformation("[Firestore] Created project history: {HistoryId} for game: {GameId}", history.Id, history.GameId);
        return history;
    }

    public async Task<ProjectHistory> UpdateAsync(ProjectHistory history)
    {
        var firestoreData = new Dictionary<string, object?>
        {
            { "GameId", history.GameId.ToString() },
            { "UserId", history.UserId },
            { "CreatedAt", history.CreatedAt },
            { "InitialUpload", ConvertToFirestore(history.InitialUpload) },
            { "Processing", ConvertToFirestore(history.Processing) },
            { "Versions", history.Versions.Select(ConvertToFirestore).ToList() }
        };
        
        var docRef = _firestoreDb.Collection(HISTORY_COLLECTION).Document(history.Id.ToString());
        await docRef.SetAsync(firestoreData, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Updated project history: {HistoryId}", history.Id);
        return history;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var docRef = _firestoreDb.Collection(HISTORY_COLLECTION).Document(id.ToString());
            await docRef.DeleteAsync();
            
            _logger.LogInformation("[Firestore] Deleted project history: {HistoryId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting project history: {HistoryId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteByGameIdAsync(Guid gameId)
    {
        try
        {
            var history = await GetByGameIdAsync(gameId);
            if (history == null) return false;
            
            return await DeleteAsync(history.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting project history for game: {GameId}", gameId);
            return false;
        }
    }

    private static Dictionary<string, object?>? ConvertToFirestore(InitialUploadData? data)
    {
        if (data == null) return null;
        
        return new Dictionary<string, object?>
        {
            { "FileName", data.FileName },
            { "FileSize", data.FileSize },
            { "FileType", data.FileType },
            { "UploadedAt", data.UploadedAt },
            { "StorageLocation", data.StorageLocation },
            { "StorageUrl", data.StorageUrl }
        };
    }

    private static Dictionary<string, object?>? ConvertToFirestore(ProcessingData? data)
    {
        if (data == null) return null;
        
        return new Dictionary<string, object?>
        {
            { "ProcessedAt", data.ProcessedAt },
            { "PgnContent", data.PgnContent },
            { "ValidationStatus", data.ValidationStatus },
            { "ProcessingTimeMs", data.ProcessingTimeMs }
        };
    }

    private static Dictionary<string, object?> ConvertToFirestore(HistoryEntry entry)
    {
        return new Dictionary<string, object?>
        {
            { "Version", entry.Version },
            { "Timestamp", entry.Timestamp },
            { "ChangeType", entry.ChangeType },
            { "Description", entry.Description },
            { "Changes", entry.Changes }
        };
    }
}
