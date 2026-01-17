using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Cloud.Firestore;

namespace ChessDecoderApi.Models;

[FirestoreData]
public class User
{
    [Key]
    [FirestoreProperty]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [FirestoreProperty]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [FirestoreProperty]
    public string Name { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public string? Picture { get; set; }
    
    [Required]
    [FirestoreProperty]
    public string Provider { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [FirestoreProperty]
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    
    // Credits for processing games
    [FirestoreProperty]
    public int Credits { get; set; } = 10; // Default 10 credits for new users
    
    // Navigation properties - NOT stored in Firestore
    [NotMapped] // For Entity Framework
    public virtual ICollection<ChessGame> ProcessedGames { get; set; } = new List<ChessGame>();
}

public class AuthRequest
{
    public string AccessToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Valid { get; set; }
    public User? User { get; set; }
    public string? Message { get; set; }
    public string? JwtToken { get; set; }
}

public class UserProfileRequest
{
    public string? Name { get; set; }
}

public class UserProfileResponse
{
    public User User { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class TestLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
