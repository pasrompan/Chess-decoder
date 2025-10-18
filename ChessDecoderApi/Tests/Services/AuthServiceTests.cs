using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Interfaces;
using ChessDecoderApi.Services;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace ChessDecoderApi.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;

    public AuthServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());
        _userRepositoryMock = new Mock<IUserRepository>();

        _repositoryFactoryMock
            .Setup(x => x.CreateUserRepositoryAsync())
            .ReturnsAsync(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task VerifyGoogleTokenAsync_ValidToken_ReturnsSuccess()
    {
        // Arrange
        var googleUserInfo = @"{
            ""id"": ""123456"",
            ""email"": ""test@example.com"",
            ""name"": ""Test User"",
            ""picture"": ""https://example.com/photo.jpg""
        }";

        var httpHandler = MockHttpMessageHandler.CreateSuccess(googleUserInfo);
        var httpClient = new HttpClient(httpHandler);
        
        var existingUser = TestDataBuilder.CreateUser(id: "123456", email: "test@example.com");
        _userRepositoryMock.Setup(x => x.GetByIdAsync("123456")).ReturnsAsync(existingUser);

        var authService = new AuthService(_configurationMock.Object, _loggerMock.Object, httpClient, _repositoryFactoryMock.Object);

        // Act
        var result = await authService.VerifyGoogleTokenAsync("valid-token");

        // Assert
        Assert.True(result.Valid);
        Assert.NotNull(result.User);
        Assert.Equal("test@example.com", result.User.Email);
        Assert.Equal("Authentication successful", result.Message);
    }

    [Fact]
    public async Task VerifyGoogleTokenAsync_InvalidToken_ReturnsFailure()
    {
        // Arrange
        var httpHandler = MockHttpMessageHandler.CreateError(HttpStatusCode.Unauthorized);
        var httpClient = new HttpClient(httpHandler);

        var authService = new AuthService(_configurationMock.Object, _loggerMock.Object, httpClient, _repositoryFactoryMock.Object);

        // Act
        var result = await authService.VerifyGoogleTokenAsync("invalid-token");

        // Assert
        Assert.False(result.Valid);
        Assert.Equal("Invalid access token", result.Message);
    }

    [Fact]
    public async Task VerifyGoogleTokenAsync_NewUser_CreatesUser()
    {
        // Arrange
        var googleUserInfo = @"{
            ""id"": ""new-user-123"",
            ""email"": ""newuser@example.com"",
            ""name"": ""New User"",
            ""picture"": ""https://example.com/photo.jpg""
        }";

        var httpHandler = MockHttpMessageHandler.CreateSuccess(googleUserInfo);
        var httpClient = new HttpClient(httpHandler);
        
        _userRepositoryMock.Setup(x => x.GetByIdAsync("new-user-123")).ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => u);

        var authService = new AuthService(_configurationMock.Object, _loggerMock.Object, httpClient, _repositoryFactoryMock.Object);

        // Act
        var result = await authService.VerifyGoogleTokenAsync("valid-token");

        // Assert
        Assert.True(result.Valid);
        _userRepositoryMock.Verify(x => x.CreateAsync(It.Is<User>(u => 
            u.Id == "new-user-123" && 
            u.Email == "newuser@example.com" &&
            u.Credits == 10)), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser();
        _userRepositoryMock.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var httpClient = new HttpClient();
        var authService = new AuthService(_configurationMock.Object, _loggerMock.Object, httpClient, _repositoryFactoryMock.Object);

        // Act
        var result = await authService.GetUserProfileAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task GetUserProfileAsync_NonExistingUser_ReturnsNull()
    {
        // Arrange
        _userRepositoryMock.Setup(x => x.GetByIdAsync("non-existing")).ReturnsAsync((User?)null);

        var httpClient = new HttpClient();
        var authService = new AuthService(_configurationMock.Object, _loggerMock.Object, httpClient, _repositoryFactoryMock.Object);

        // Act
        var result = await authService.GetUserProfileAsync("non-existing");

        // Assert
        Assert.Null(result);
    }
}

