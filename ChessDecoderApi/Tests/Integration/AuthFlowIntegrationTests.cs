using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Sqlite;
using ChessDecoderApi.Services;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace ChessDecoderApi.Tests.Integration;

/// <summary>
/// Integration tests for authentication flow with real database operations
/// </summary>
public class AuthFlowIntegrationTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;

    public AuthFlowIntegrationTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    [Fact]
    public async Task AuthFlow_NewUser_CreatesUserWithInitialCredits()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var userRepo = new SqliteUserRepository(context, Mock.Of<ILogger<SqliteUserRepository>>());
        var repositoryFactory = CreateRepositoryFactory(userRepo);

        var googleUserInfo = @"{
            ""id"": ""new-google-user-123"",
            ""email"": ""newuser@gmail.com"",
            ""name"": ""New User"",
            ""picture"": ""https://example.com/photo.jpg""
        }";
        var httpHandler = MockHttpMessageHandler.CreateSuccess(googleUserInfo);
        var httpClient = new HttpClient(httpHandler);

        var authService = new AuthService(
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<AuthService>>(),
            httpClient,
            repositoryFactory
        );

        var creditService = new CreditService(
            repositoryFactory,
            Mock.Of<ILogger<CreditService>>()
        );

        // Act - Authenticate new user
        var authResult = await authService.VerifyGoogleTokenAsync("valid-token");

        // Assert - User created successfully
        Assert.True(authResult.Valid);
        Assert.NotNull(authResult.User);
        Assert.Equal("newuser@gmail.com", authResult.User.Email);
        Assert.Equal("new-google-user-123", authResult.User.Id);

        // Assert - User has initial credits
        Assert.Equal(10, authResult.User.Credits);

        // Verify via credit service
        var credits = await creditService.GetUserCreditsAsync("new-google-user-123");
        Assert.Equal(10, credits);
    }

    [Fact]
    public async Task AuthFlow_ExistingUser_UpdatesLastLogin()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var userRepo = new SqliteUserRepository(context, Mock.Of<ILogger<SqliteUserRepository>>());
        
        // Create existing user
        var existingUser = TestDataBuilder.CreateUser(
            id: "existing-google-user",
            email: "existing@gmail.com"
        );
        existingUser.LastLoginAt = DateTime.UtcNow.AddDays(-7);
        await userRepo.CreateAsync(existingUser);

        var repositoryFactory = CreateRepositoryFactory(userRepo);

        var googleUserInfo = @"{
            ""id"": ""existing-google-user"",
            ""email"": ""existing@gmail.com"",
            ""name"": ""Existing User"",
            ""picture"": ""https://example.com/photo.jpg""
        }";
        var httpHandler = MockHttpMessageHandler.CreateSuccess(googleUserInfo);
        var httpClient = new HttpClient(httpHandler);

        var authService = new AuthService(
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<AuthService>>(),
            httpClient,
            repositoryFactory
        );

        var oldLoginTime = existingUser.LastLoginAt;

        // Act - Authenticate existing user
        await Task.Delay(10); // Ensure time difference
        var authResult = await authService.VerifyGoogleTokenAsync("valid-token");

        // Assert
        Assert.True(authResult.Valid);
        Assert.NotNull(authResult.User);
        Assert.True(authResult.User.LastLoginAt > oldLoginTime);
    }

    [Fact]
    public async Task CreditFlow_UserProcessesGame_CreditsDeductedCorrectly()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var userRepo = new SqliteUserRepository(context, Mock.Of<ILogger<SqliteUserRepository>>());
        
        var user = TestDataBuilder.CreateUser(id: "game-user", credits: 10);
        await userRepo.CreateAsync(user);

        var repositoryFactory = CreateRepositoryFactory(userRepo);
        var creditService = new CreditService(repositoryFactory, Mock.Of<ILogger<CreditService>>());

        // Act - Check initial credits
        var initialCredits = await creditService.GetUserCreditsAsync("game-user");
        Assert.Equal(10, initialCredits);

        // Act - Deduct credits for game processing
        var deducted = await creditService.DeductCreditsAsync("game-user", 1);
        Assert.True(deducted);

        // Assert - Credits reduced
        var remainingCredits = await creditService.GetUserCreditsAsync("game-user");
        Assert.Equal(9, remainingCredits);

        // Act - Deduct more credits
        await creditService.DeductCreditsAsync("game-user", 5);
        remainingCredits = await creditService.GetUserCreditsAsync("game-user");
        Assert.Equal(4, remainingCredits);

        // Act - Try to deduct more than available
        var insufficientDeduction = await creditService.DeductCreditsAsync("game-user", 10);
        Assert.False(insufficientDeduction);

        // Assert - Credits unchanged after failed deduction
        remainingCredits = await creditService.GetUserCreditsAsync("game-user");
        Assert.Equal(4, remainingCredits);
    }

    [Fact]
    public async Task CreditFlow_RefundCredits_WorksCorrectly()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var userRepo = new SqliteUserRepository(context, Mock.Of<ILogger<SqliteUserRepository>>());
        
        var user = TestDataBuilder.CreateUser(id: "refund-user", credits: 5);
        await userRepo.CreateAsync(user);

        var repositoryFactory = CreateRepositoryFactory(userRepo);
        var creditService = new CreditService(repositoryFactory, Mock.Of<ILogger<CreditService>>());

        // Act - Refund credits
        var refunded = await creditService.RefundCreditsAsync("refund-user", 3);
        Assert.True(refunded);

        // Assert
        var newBalance = await creditService.GetUserCreditsAsync("refund-user");
        Assert.Equal(8, newBalance);
    }

    private RepositoryFactory CreateRepositoryFactory(SqliteUserRepository userRepo)
    {
        var firestoreServiceMock = new Mock<IFirestoreService>();
        firestoreServiceMock.Setup(x => x.IsAvailableAsync()).ReturnsAsync(false);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ChessDecoderApi.Data.ChessDecoderDbContext)))
            .Returns(_dbFactory.CreateContext());
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<SqliteUserRepository>)))
            .Returns(Mock.Of<ILogger<SqliteUserRepository>>());
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<SqliteChessGameRepository>)))
            .Returns(Mock.Of<ILogger<SqliteChessGameRepository>>());
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<SqliteGameImageRepository>)))
            .Returns(Mock.Of<ILogger<SqliteGameImageRepository>>());
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<SqliteGameStatisticsRepository>)))
            .Returns(Mock.Of<ILogger<SqliteGameStatisticsRepository>>());

        return new RepositoryFactory(
            serviceProviderMock.Object,
            firestoreServiceMock.Object,
            Mock.Of<ILogger<RepositoryFactory>>()
        );
    }

    public void Dispose()
    {
        _dbFactory?.Dispose();
    }
}

