namespace ChessDecoderApi.Models;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
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
