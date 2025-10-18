using Google.Cloud.Firestore;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Firestore;

/// <summary>
/// Firestore implementation of IGameImageRepository
/// </summary>
public class FirestoreGameImageRepository : IGameImageRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirestoreGameImageRepository> _logger;
    private const string IMAGES_COLLECTION = "game_images";

    public FirestoreGameImageRepository(FirestoreDb firestoreDb, ILogger<FirestoreGameImageRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameImage?> GetByIdAsync(Guid id)
    {
        var docRef = _firestoreDb.Collection(IMAGES_COLLECTION).Document(id.ToString());
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

    public async Task<List<GameImage>> GetByChessGameIdAsync(Guid chessGameId)
    {
        var query = _firestoreDb.Collection(IMAGES_COLLECTION)
            .WhereEqualTo("chessGameId", chessGameId.ToString());
        
        var snapshot = await query.GetSnapshotAsync();
        
        return snapshot.Documents.Select(doc =>
        {
            var image = doc.ConvertTo<GameImage>();
            image.Id = Guid.Parse(doc.Id);
            
            // Manually parse chessGameId from string to Guid
            if (doc.TryGetValue<string>("chessGameId", out var chessGameIdStr) &&
                Guid.TryParse(chessGameIdStr, out var parsedGameId))
            {
                image.ChessGameId = parsedGameId;
            }
            
            return image;
        }).ToList();
    }

    public async Task<GameImage> CreateAsync(GameImage image)
    {
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
        
        var docRef = _firestoreDb.Collection(IMAGES_COLLECTION).Document(image.Id.ToString());
        await docRef.SetAsync(firestoreData);
        
        _logger.LogInformation("[Firestore] Created image: {ImageId}", image.Id);
        return image;
    }

    public async Task<GameImage> UpdateAsync(GameImage image)
    {
        var firestoreData = new Dictionary<string, object?>
        {
            { "chessGameId", image.ChessGameId.ToString() },
            { "FileName", image.FileName },
            { "FilePath", image.FilePath },
            { "FileType", image.FileType ?? "" },
            { "FileSizeBytes", image.FileSizeBytes },
            { "UploadedAt", image.UploadedAt },
            { "CloudStorageUrl", image.CloudStorageUrl ?? "" },
            { "CloudStorageObjectName", image.CloudStorageObjectName ?? "" },
            { "IsStoredInCloud", image.IsStoredInCloud }
        };
        
        var docRef = _firestoreDb.Collection(IMAGES_COLLECTION).Document(image.Id.ToString());
        await docRef.SetAsync(firestoreData, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Updated image: {ImageId}", image.Id);
        return image;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var docRef = _firestoreDb.Collection(IMAGES_COLLECTION).Document(id.ToString());
            await docRef.DeleteAsync();
            
            _logger.LogInformation("[Firestore] Deleted image: {ImageId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting image: {ImageId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteByChessGameIdAsync(Guid chessGameId)
    {
        try
        {
            var images = await GetByChessGameIdAsync(chessGameId);
            foreach (var image in images)
            {
                await DeleteAsync(image.Id);
            }
            
            _logger.LogInformation("[Firestore] Deleted all images for game: {GameId}", chessGameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting images for game: {GameId}", chessGameId);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        var docRef = _firestoreDb.Collection(IMAGES_COLLECTION).Document(id.ToString());
        var snapshot = await docRef.GetSnapshotAsync();
        return snapshot.Exists;
    }
}

