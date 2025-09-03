using ChessDecoderApi.Models;
using ChessDecoderApi.Data;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ChessDecoderApi.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ChessDecoderDbContext _context;

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger, HttpClient httpClient, ChessDecoderDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
        _context = context;
    }

    public async Task<AuthResponse> VerifyGoogleTokenAsync(string accessToken)
    {
        try
        {
            _logger.LogInformation("Verifying Google access token");

            // Get user info from Google using the access token
            var userInfoResponse = await _httpClient.GetAsync(
                $"https://www.googleapis.com/oauth2/v2/userinfo?access_token={accessToken}"
            );

            if (!userInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user info from Google: {StatusCode}", userInfoResponse.StatusCode);
                return new AuthResponse
                {
                    Valid = false,
                    Message = "Invalid access token"
                };
            }

            var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<JsonElement>(userInfoJson);

            // Extract user information
            var userId = userInfo.GetProperty("id").GetString() ?? string.Empty;
            var email = userInfo.GetProperty("email").GetString() ?? string.Empty;
            var name = userInfo.GetProperty("name").GetString() ?? string.Empty;
            var picture = userInfo.GetProperty("picture").GetString();

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Invalid user info from Google");
                return new AuthResponse
                {
                    Valid = false,
                    Message = "Invalid user information from Google"
                };
            }

            // Check if user exists, create if not
            var user = await GetOrCreateUserAsync(userId, email, name, picture);

            _logger.LogInformation("User authenticated successfully: {Email}", email);

            return new AuthResponse
            {
                Valid = true,
                User = user,
                Message = "Authentication successful"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Google token");
            return new AuthResponse
            {
                Valid = false,
                Message = "Error during authentication"
            };
        }
    }

    public async Task<User?> GetUserProfileAsync(string userId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User> UpdateUserProfileAsync(string userId, UserProfileRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            user.Name = request.Name;
        }

        await _context.SaveChangesAsync();
        return user;
    }

    public Task<bool> SignOutUserAsync(string userId)
    {
        // In a real application, you might want to:
        // 1. Invalidate JWT tokens
        // 2. Log the sign-out event
        // 3. Clear any server-side sessions
        
        _logger.LogInformation("User signed out: {UserId}", userId);
        return Task.FromResult(true);
    }

    private async Task<User> GetOrCreateUserAsync(string userId, string email, string name, string? picture)
    {
        // Check if user exists in database
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        
        if (existingUser != null)
        {
            // Update last login time
            existingUser.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return existingUser;
        }

        // Create new user
        var newUser = new User
        {
            Id = userId,
            Email = email,
            Name = name,
            Picture = picture,
            Provider = "google",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            Credits = 10 // Default credits for new users
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created new user in database: {Email}", email);
        return newUser;
    }
}
