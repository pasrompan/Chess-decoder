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

        return MapDocumentToImage(snapshot);
    }

    public async Task<List<GameImage>> GetByChessGameIdAsync(Guid chessGameId)
    {
        var query = _firestoreDb.Collection(IMAGES_COLLECTION)
            .WhereEqualTo("chessGameId", chessGameId.ToString());
        
        var snapshot = await query.GetSnapshotAsync();

        return snapshot.Documents
            .Select(MapDocumentToImage)
            .ToList();
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
            { "IsStoredInCloud", image.IsStoredInCloud },
            { "Variant", image.Variant },
            { "PageNumber", image.PageNumber },
            { "StartingMoveNumber", image.StartingMoveNumber },
            { "EndingMoveNumber", image.EndingMoveNumber },
            { "ContinuationImageId", image.ContinuationImageId?.ToString() ?? string.Empty }
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
            { "IsStoredInCloud", image.IsStoredInCloud },
            { "Variant", image.Variant },
            { "PageNumber", image.PageNumber },
            { "StartingMoveNumber", image.StartingMoveNumber },
            { "EndingMoveNumber", image.EndingMoveNumber },
            { "ContinuationImageId", image.ContinuationImageId?.ToString() ?? string.Empty }
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

    private static GameImage MapDocumentToImage(DocumentSnapshot doc)
    {
        Guid.TryParse(doc.Id, out var parsedImageId);

        var image = new GameImage
        {
            Id = parsedImageId,
            ChessGameId = ReadGuid(doc, "chessGameId") ?? Guid.Empty,
            FileName = ReadString(doc, "FileName") ?? string.Empty,
            FilePath = ReadString(doc, "FilePath") ?? string.Empty,
            FileType = ReadString(doc, "FileType"),
            FileSizeBytes = ReadInt64(doc, "FileSizeBytes"),
            UploadedAt = ReadDateTime(doc, "UploadedAt"),
            CloudStorageUrl = ReadString(doc, "CloudStorageUrl"),
            CloudStorageObjectName = ReadString(doc, "CloudStorageObjectName"),
            IsStoredInCloud = ReadBoolean(doc, "IsStoredInCloud"),
            Variant = ReadString(doc, "Variant") ?? "original",
            PageNumber = Math.Max(1, ReadInt32(doc, "PageNumber", 1)),
            StartingMoveNumber = ReadInt32(doc, "StartingMoveNumber"),
            EndingMoveNumber = ReadInt32(doc, "EndingMoveNumber"),
            ContinuationImageId = ReadGuid(doc, "ContinuationImageId")
        };

        return image;
    }

    private static string? ReadString(DocumentSnapshot doc, string fieldName)
    {
        if (doc.TryGetValue<string>(fieldName, out var value))
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static bool ReadBoolean(DocumentSnapshot doc, string fieldName, bool fallback = false)
    {
        if (doc.TryGetValue<bool>(fieldName, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static int ReadInt32(DocumentSnapshot doc, string fieldName, int fallback = 0)
    {
        if (doc.TryGetValue<int>(fieldName, out var intValue))
        {
            return intValue;
        }

        if (doc.TryGetValue<long>(fieldName, out var longValue))
        {
            return (int)longValue;
        }

        return fallback;
    }

    private static long ReadInt64(DocumentSnapshot doc, string fieldName, long fallback = 0L)
    {
        if (doc.TryGetValue<long>(fieldName, out var longValue))
        {
            return longValue;
        }

        if (doc.TryGetValue<int>(fieldName, out var intValue))
        {
            return intValue;
        }

        return fallback;
    }

    private static DateTime ReadDateTime(DocumentSnapshot doc, string fieldName)
    {
        if (doc.TryGetValue<DateTime>(fieldName, out var value))
        {
            return value;
        }

        if (doc.TryGetValue<Timestamp>(fieldName, out var timestamp))
        {
            return timestamp.ToDateTime();
        }

        return DateTime.UtcNow;
    }

    private static Guid? ReadGuid(DocumentSnapshot doc, string fieldName)
    {
        if (!doc.TryGetValue<string>(fieldName, out var value))
        {
            return null;
        }

        if (Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        return null;
    }
}
