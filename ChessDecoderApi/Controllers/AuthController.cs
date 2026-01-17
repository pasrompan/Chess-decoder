using ChessDecoderApi.Models;
using ChessDecoderApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessDecoderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, ILogger<AuthController> logger, IConfiguration configuration)
    {
        _authService = authService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Verify Google OAuth access token and authenticate user
    /// </summary>
    /// <param name="request">Contains the Google access token</param>
    /// <returns>Authentication result with user information</returns>
    [HttpPost("verify")]
    public async Task<ActionResult<AuthResponse>> VerifyToken([FromBody] AuthRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.AccessToken))
            {
                return BadRequest(new AuthResponse
                {
                    Valid = false,
                    Message = "Access token is required"
                });
            }

            var result = await _authService.VerifyGoogleTokenAsync(request.AccessToken);
            
            if (result.Valid)
            {
                _logger.LogInformation("User authenticated successfully: {Email}", result.User?.Email);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Authentication failed: {Message}", result.Message);
                return Unauthorized(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token verification");
            return StatusCode(500, new AuthResponse
            {
                Valid = false,
                Message = "Internal server error during authentication"
            });
        }
    }

    /// <summary>
    /// Test login endpoint for E2E testing (only available when ENABLE_TEST_AUTH=true)
    /// </summary>
    /// <param name="request">Contains email and password for test authentication</param>
    /// <returns>Authentication result with user information</returns>
    [HttpPost("test-login")]
    public async Task<ActionResult<AuthResponse>> TestLogin([FromBody] TestLoginRequest request)
    {
        // Check if test auth is enabled - return 404 to hide endpoint existence in production
        var testAuthEnabled = _configuration.GetValue<bool>("ENABLE_TEST_AUTH", false) ||
                              Environment.GetEnvironmentVariable("ENABLE_TEST_AUTH")?.ToLower() == "true";

        if (!testAuthEnabled)
        {
            _logger.LogWarning("Test login endpoint accessed but ENABLE_TEST_AUTH is not enabled");
            return NotFound();
        }

        try
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new AuthResponse
                {
                    Valid = false,
                    Message = "Email and password are required"
                });
            }

            var result = await _authService.VerifyTestCredentialsAsync(request.Email, request.Password);

            if (result.Valid)
            {
                _logger.LogInformation("Test user authenticated successfully: {Email}", request.Email);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Test authentication failed: {Message}", result.Message);
                return Unauthorized(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test login");
            return StatusCode(500, new AuthResponse
            {
                Valid = false,
                Message = "Internal server error during test authentication"
            });
        }
    }

    /// <summary>
    /// Get user profile information
    /// </summary>
    /// <param name="userId">User ID from the authenticated user</param>
    /// <returns>User profile information</returns>
    [HttpGet("profile")]
    public async Task<ActionResult<User>> GetProfile([FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            var user = await _authService.GetUserProfileAsync(userId);
            
            if (user == null)
            {
                return NotFound("User not found");
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile for user: {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update user profile information
    /// </summary>
    /// <param name="userId">User ID from the authenticated user</param>
    /// <param name="request">Profile update request</param>
    /// <returns>Updated user profile</returns>
    [HttpPut("profile")]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromQuery] string userId, 
        [FromBody] UserProfileRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            var updatedUser = await _authService.UpdateUserProfileAsync(userId, request);
            
            return Ok(new UserProfileResponse
            {
                User = updatedUser,
                Message = "Profile updated successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile for user: {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Sign out user
    /// </summary>
    /// <param name="userId">User ID from the authenticated user</param>
    /// <returns>Sign out result</returns>
    [HttpPost("signout")]
    public async Task<ActionResult> SignOut([FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            var result = await _authService.SignOutUserAsync(userId);
            
            if (result)
            {
                _logger.LogInformation("User signed out successfully: {UserId}", userId);
                return Ok(new { message = "Signed out successfully" });
            }
            else
            {
                return BadRequest("Failed to sign out");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sign out for user: {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }
}
