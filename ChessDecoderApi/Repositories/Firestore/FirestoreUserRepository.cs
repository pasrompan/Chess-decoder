using Google.Cloud.Firestore;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Firestore;

/// <summary>
/// Firestore implementation of IUserRepository
/// </summary>
public class FirestoreUserRepository : IUserRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirestoreUserRepository> _logger;
    private const string USERS_COLLECTION = "users";

    public FirestoreUserRepository(FirestoreDb firestoreDb, ILogger<FirestoreUserRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        var docRef = _firestoreDb.Collection(USERS_COLLECTION).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return null;
        
        var user = snapshot.ConvertTo<User>();
        user.Id = snapshot.Id;
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var query = _firestoreDb.Collection(USERS_COLLECTION).WhereEqualTo("Email", email).Limit(1);
        var snapshot = await query.GetSnapshotAsync();
        
        var doc = snapshot.Documents.FirstOrDefault();
        if (doc == null) return null;
        
        var user = doc.ConvertTo<User>();
        user.Id = doc.Id;
        return user;
    }

    public async Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.LastLoginAt = DateTime.UtcNow;
        
        var docRef = _firestoreDb.Collection(USERS_COLLECTION).Document(user.Id);
        await docRef.SetAsync(user);
        
        _logger.LogInformation("[Firestore] Created user: {UserId}", user.Id);
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        var docRef = _firestoreDb.Collection(USERS_COLLECTION).Document(user.Id);
        await docRef.SetAsync(user, SetOptions.MergeAll);
        
        _logger.LogInformation("[Firestore] Updated user: {UserId}", user.Id);
        return user;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            var docRef = _firestoreDb.Collection(USERS_COLLECTION).Document(id);
            await docRef.DeleteAsync();
            
            _logger.LogInformation("[Firestore] Deleted user: {UserId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error deleting user: {UserId}", id);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        var docRef = _firestoreDb.Collection(USERS_COLLECTION).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();
        return snapshot.Exists;
    }

    public async Task<int> GetCreditsAsync(string userId)
    {
        var user = await GetByIdAsync(userId);
        return user?.Credits ?? 0;
    }

    public async Task<bool> UpdateCreditsAsync(string userId, int credits)
    {
        try
        {
            var docRef = _firestoreDb.Collection(USERS_COLLECTION).Document(userId);
            await docRef.UpdateAsync("Credits", credits);
            
            _logger.LogInformation("[Firestore] Updated credits for user {UserId} to {Credits}", userId, credits);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore] Error updating credits for user: {UserId}", userId);
            return false;
        }
    }
}

