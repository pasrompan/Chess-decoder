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
    private readonly IFirestoreService _firestore;
    private readonly ChessDecoderDbContext _context;

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger, HttpClient httpClient, IFirestoreService firestore, ChessDecoderDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
        _firestore = firestore;
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
        // Try Firestore first, fall back to EF
        if (await _firestore.IsAvailableAsync())
        {
            _logger.LogDebug("[Auth] Using Firestore to get user profile: {UserId}", userId);
            return await _firestore.GetUserByIdAsync(userId);
        }
        
        _logger.LogDebug("[Auth] Using SQLite/EF to get user profile: {UserId}", userId);
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User> UpdateUserProfileAsync(string userId, UserProfileRequest request)
    {
        // Try Firestore first, fall back to EF
        if (await _firestore.IsAvailableAsync())
        {
            _logger.LogDebug("[Auth] Using Firestore to update user profile: {UserId}", userId);
            var user = await _firestore.GetUserByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            if (!string.IsNullOrEmpty(request.Name))
            {
                user.Name = request.Name;
            }

            return await _firestore.UpdateUserAsync(user);
        }
        
        _logger.LogDebug("[Auth] Using SQLite/EF to update user profile: {UserId}", userId);
        var efUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (efUser == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            efUser.Name = request.Name;
        }

        await _context.SaveChangesAsync();
        return efUser;
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
        // Try Firestore first, fall back to EF
        if (await _firestore.IsAvailableAsync())
        {
            _logger.LogDebug("[Auth] Using Firestore for GetOrCreateUser");
            
            // Check if user exists in Firestore
            var existingUser = await _firestore.GetUserByIdAsync(userId);
            
            if (existingUser != null)
            {
                // Update last login time
                existingUser.LastLoginAt = DateTime.UtcNow;
                await _firestore.UpdateUserAsync(existingUser);
                return existingUser;
            }

            // Create new user in Firestore
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

            await _firestore.CreateUserAsync(newUser);
            _logger.LogInformation("Created new user in Firestore: {Email}", email);
            return newUser;
        }
        
        // Fallback to EF/SQLite
        _logger.LogDebug("[Auth] Using SQLite/EF for GetOrCreateUser");
        var efExistingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        
        if (efExistingUser != null)
        {
            // Update last login time
            efExistingUser.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return efExistingUser;
        }

        // Create new user in EF/SQLite
        var efNewUser = new User
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

        _context.Users.Add(efNewUser);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created new user in SQLite/EF: {Email}", email);
        return efNewUser;
    }
}
