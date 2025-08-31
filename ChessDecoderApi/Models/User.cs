using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessDecoderApi.Models;

public class User
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Picture { get; set; }
    
    [Required]
    public string Provider { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    
    // Credits for processing games
    public int Credits { get; set; } = 10; // Default 10 credits for new users
    
    // Navigation properties
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
