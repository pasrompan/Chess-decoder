using ChessDecoderApi.Controllers;
using ChessDecoderApi.Models;
using ChessDecoderApi.Services;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_authServiceMock.Object, _loggerMock.Object);
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
}

