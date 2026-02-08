using ChessDecoderApi.Controllers;
using ChessDecoderApi.Models;
using ChessDecoderApi.Services;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        
        // Use in-memory configuration for testing
        var configValues = new Dictionary<string, string?>
        {
            { "ENABLE_TEST_AUTH", "false" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
            
        _controller = new AuthController(_authServiceMock.Object, _loggerMock.Object, _configuration);
    }
    
    private AuthController CreateControllerWithTestAuthEnabled()
    {
        var configValues = new Dictionary<string, string?>
        {
            { "ENABLE_TEST_AUTH", "true" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        return new AuthController(_authServiceMock.Object, _loggerMock.Object, config);
    }
    
    private void SetDevelopmentEnvironment()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }
    
    private void RestoreEnvironment(string? originalValue)
    {
        if (originalValue == null)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalValue);
        }
    }

    [Fact]
    public async Task VerifyToken_ValidToken_ReturnsOk()
    {
        // Arrange
        var request = new AuthRequest { AccessToken = "valid-token" };
        var user = TestDataBuilder.CreateUser();
        var authResponse = new AuthResponse
        {
            Valid = true,
            User = user,
            Message = "Authentication successful"
        };
        _authServiceMock.Setup(x => x.VerifyGoogleTokenAsync(request.AccessToken)).ReturnsAsync(authResponse);

        // Act
        var result = await _controller.VerifyToken(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.True(response.Valid);
        Assert.NotNull(response.User);
    }

    [Fact]
    public async Task VerifyToken_InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AuthRequest { AccessToken = "invalid-token" };
        var authResponse = new AuthResponse
        {
            Valid = false,
            Message = "Invalid access token"
        };
        _authServiceMock.Setup(x => x.VerifyGoogleTokenAsync(request.AccessToken)).ReturnsAsync(authResponse);

        // Act
        var result = await _controller.VerifyToken(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponse>(unauthorizedResult.Value);
        Assert.False(response.Valid);
    }

    [Fact]
    public async Task VerifyToken_EmptyToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new AuthRequest { AccessToken = "" };

        // Act
        var result = await _controller.VerifyToken(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(response.Valid);
    }

    [Fact]
    public async Task VerifyToken_Exception_ReturnsInternalServerError()
    {
        // Arrange
        var request = new AuthRequest { AccessToken = "token" };
        _authServiceMock.Setup(x => x.VerifyGoogleTokenAsync(request.AccessToken))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.VerifyToken(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetProfile_ExistingUser_ReturnsOk()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser();
        _authServiceMock.Setup(x => x.GetUserProfileAsync(user.Id)).ReturnsAsync(user);

        // Act
        var result = await _controller.GetProfile(user.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUser = Assert.IsType<User>(okResult.Value);
        Assert.Equal(user.Id, returnedUser.Id);
    }

    [Fact]
    public async Task GetProfile_NonExistingUser_ReturnsNotFound()
    {
        // Arrange
        var userId = "non-existing";
        _authServiceMock.Setup(x => x.GetUserProfileAsync(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _controller.GetProfile(userId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetProfile_EmptyUserId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetProfile("");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task TestLogin_WhenDisabled_ReturnsNotFound()
    {
        // Arrange - Test auth is disabled by default
        var request = new TestLoginRequest 
        { 
            Email = "test@chessscribe.local", 
            Password = "testpassword123" 
        };

        // Act
        var result = await _controller.TestLogin(request);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task TestLogin_WhenEnabled_ValidCredentials_ReturnsOk()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        SetDevelopmentEnvironment();
        try
        {
            var controller = CreateControllerWithTestAuthEnabled();
            
            var request = new TestLoginRequest 
            { 
                Email = "test@chessscribe.local", 
                Password = "testpassword123" 
            };
            var user = TestDataBuilder.CreateUser();
            user.Provider = "test";
            var authResponse = new AuthResponse
            {
                Valid = true,
                User = user,
                Message = "Test authentication successful"
            };
            _authServiceMock.Setup(x => x.VerifyTestCredentialsAsync(request.Email, request.Password))
                .ReturnsAsync(authResponse);

            // Act
            var result = await controller.TestLogin(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponse>(okResult.Value);
            Assert.True(response.Valid);
            Assert.NotNull(response.User);
        }
        finally
        {
            RestoreEnvironment(originalEnv);
        }
    }

    [Fact]
    public async Task TestLogin_WhenEnabled_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        SetDevelopmentEnvironment();
        try
        {
            var controller = CreateControllerWithTestAuthEnabled();
            
            var request = new TestLoginRequest 
            { 
                Email = "wrong@email.com", 
                Password = "wrongpassword" 
            };
            var authResponse = new AuthResponse
            {
                Valid = false,
                Message = "Invalid credentials"
            };
            _authServiceMock.Setup(x => x.VerifyTestCredentialsAsync(request.Email, request.Password))
                .ReturnsAsync(authResponse);

            // Act
            var result = await controller.TestLogin(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponse>(unauthorizedResult.Value);
            Assert.False(response.Valid);
        }
        finally
        {
            RestoreEnvironment(originalEnv);
        }
    }

    [Fact]
    public async Task TestLogin_WhenEnabled_EmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        SetDevelopmentEnvironment();
        try
        {
            var controller = CreateControllerWithTestAuthEnabled();
            
            var request = new TestLoginRequest 
            { 
                Email = "", 
                Password = "" 
            };

            // Act
            var result = await controller.TestLogin(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponse>(badRequestResult.Value);
            Assert.False(response.Valid);
        }
        finally
        {
            RestoreEnvironment(originalEnv);
        }
    }
}

