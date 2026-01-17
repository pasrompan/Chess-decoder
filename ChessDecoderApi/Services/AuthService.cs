using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChessDecoderApi.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly HttpClient _httpClient;
    private readonly RepositoryFactory _repositoryFactory;

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger, HttpClient httpClient, RepositoryFactory repositoryFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
        _repositoryFactory = repositoryFactory;
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
        var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
        return await userRepo.GetByIdAsync(userId);
    }

    public async Task<User> UpdateUserProfileAsync(string userId, UserProfileRequest request)
    {
        var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
        var user = await userRepo.GetByIdAsync(userId);
        
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            user.Name = request.Name;
        }

        return await userRepo.UpdateAsync(user);
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

    public async Task<AuthResponse> VerifyTestCredentialsAsync(string email, string password)
    {
        // Check if test auth is enabled
        var testAuthEnabled = _configuration.GetValue<bool>("ENABLE_TEST_AUTH", false) ||
                              Environment.GetEnvironmentVariable("ENABLE_TEST_AUTH")?.ToLower() == "true";

        if (!testAuthEnabled)
        {
            _logger.LogWarning("Test authentication attempted but ENABLE_TEST_AUTH is not enabled");
            return new AuthResponse
            {
                Valid = false,
                Message = "Test authentication is not available"
            };
        }

        // Validate test credentials
        const string testEmail = "test@chessscribe.local";
        const string testPassword = "testpassword123";

        if (email != testEmail || password != testPassword)
        {
            _logger.LogWarning("Invalid test credentials provided for email: {Email}", email);
            return new AuthResponse
            {
                Valid = false,
                Message = "Invalid credentials"
            };
        }

        // Create a deterministic user ID for test user
        var testUserId = "test-user-e2e-" + email.GetHashCode().ToString("X");
        
        // Get or create the test user
        var user = await GetOrCreateTestUserAsync(testUserId, email, "Test User");

        _logger.LogInformation("Test user authenticated successfully: {Email}", email);

        return new AuthResponse
        {
            Valid = true,
            User = user,
            Message = "Test authentication successful"
        };
    }

    private async Task<User> GetOrCreateTestUserAsync(string userId, string email, string name)
    {
        var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
        
        // Check if user exists
        var existingUser = await userRepo.GetByIdAsync(userId);
        
        if (existingUser != null)
        {
            // Update last login time
            existingUser.LastLoginAt = DateTime.UtcNow;
            await userRepo.UpdateAsync(existingUser);
            return existingUser;
        }

        // Create new test user
        var newUser = new User
        {
            Id = userId,
            Email = email,
            Name = name,
            Picture = null,
            Provider = "test",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            Credits = 1000 // Give test user plenty of credits for testing
        };

        await userRepo.CreateAsync(newUser);
        _logger.LogInformation("Created new test user: {Email}", email);
        return newUser;
    }

    private async Task<User> GetOrCreateUserAsync(string userId, string email, string name, string? picture)
    {
        var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
        
        // Check if user exists
        var existingUser = await userRepo.GetByIdAsync(userId);
        
        if (existingUser != null)
        {
            // Update last login time
            existingUser.LastLoginAt = DateTime.UtcNow;
            await userRepo.UpdateAsync(existingUser);
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
            Credits = 200 // Default credits for new users
        };

        await userRepo.CreateAsync(newUser);
        _logger.LogInformation("Created new user: {Email}", email);
        return newUser;
    }
}
