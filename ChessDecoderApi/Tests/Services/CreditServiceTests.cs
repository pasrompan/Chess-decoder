using ChessDecoderApi.Exceptions;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Interfaces;
using ChessDecoderApi.Services;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services;

public class CreditServiceTests
{
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<CreditService>> _loggerMock;
    private readonly CreditService _creditService;

    public CreditServiceTests()
    {
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<CreditService>>();

        _repositoryFactoryMock
            .Setup(x => x.CreateUserRepositoryAsync())
            .ReturnsAsync(_userRepositoryMock.Object);

        _creditService = new CreditService(_repositoryFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HasEnoughCreditsAsync_SufficientCredits_ReturnsTrue()
    {
        // Arrange
        var userId = "test-user";
        _userRepositoryMock.Setup(x => x.GetCreditsAsync(userId)).ReturnsAsync(10);

        // Act
        var result = await _creditService.HasEnoughCreditsAsync(userId, 5);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasEnoughCreditsAsync_InsufficientCredits_ReturnsFalse()
    {
        // Arrange
        var userId = "test-user";
        _userRepositoryMock.Setup(x => x.GetCreditsAsync(userId)).ReturnsAsync(3);

        // Act
        var result = await _creditService.HasEnoughCreditsAsync(userId, 5);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasEnoughCreditsAsync_ExactCredits_ReturnsTrue()
    {
        // Arrange
        var userId = "test-user";
        _userRepositoryMock.Setup(x => x.GetCreditsAsync(userId)).ReturnsAsync(5);

        // Act
        var result = await _creditService.HasEnoughCreditsAsync(userId, 5);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeductCreditsAsync_SufficientCredits_DeductsAndReturnsTrue()
    {
        // Arrange
        var userId = "test-user";
        var user = TestDataBuilder.CreateUser(id: userId, credits: 10);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(user);

        // Act
        var result = await _creditService.DeductCreditsAsync(userId, 3);

        // Assert
        Assert.True(result);
        Assert.Equal(7, user.Credits);
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => u.Credits == 7)), Times.Once);
    }

    [Fact]
    public async Task DeductCreditsAsync_InsufficientCredits_ReturnsFalse()
    {
        // Arrange
        var userId = "test-user";
        var user = TestDataBuilder.CreateUser(id: userId, credits: 2);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _creditService.DeductCreditsAsync(userId, 5);

        // Assert
        Assert.False(result);
        Assert.Equal(2, user.Credits); // Credits unchanged
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task DeductCreditsAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var userId = "non-existing-user";
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _creditService.DeductCreditsAsync(userId, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetUserCreditsAsync_ExistingUser_ReturnsCredits()
    {
        // Arrange
        var userId = "test-user";
        _userRepositoryMock.Setup(x => x.GetCreditsAsync(userId)).ReturnsAsync(25);
        _userRepositoryMock.Setup(x => x.ExistsAsync(userId)).ReturnsAsync(true);

        // Act
        var result = await _creditService.GetUserCreditsAsync(userId);

        // Assert
        Assert.Equal(25, result);
    }

    [Fact]
    public async Task GetUserCreditsAsync_NonExistingUser_ThrowsUserNotFoundException()
    {
        // Arrange
        var userId = "non-existing-user";
        _userRepositoryMock.Setup(x => x.GetCreditsAsync(userId)).ReturnsAsync(0);
        _userRepositoryMock.Setup(x => x.ExistsAsync(userId)).ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(() => _creditService.GetUserCreditsAsync(userId));
    }

    [Fact]
    public async Task AddCreditsAsync_ExistingUser_AddsCreditsAndReturnsTrue()
    {
        // Arrange
        var userId = "test-user";
        var user = TestDataBuilder.CreateUser(id: userId, credits: 10);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(user);

        // Act
        var result = await _creditService.AddCreditsAsync(userId, 5);

        // Assert
        Assert.True(result);
        Assert.Equal(15, user.Credits);
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => u.Credits == 15)), Times.Once);
    }

    [Fact]
    public async Task AddCreditsAsync_NonExistingUser_ThrowsUserNotFoundException()
    {
        // Arrange
        var userId = "non-existing-user";
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(() => _creditService.AddCreditsAsync(userId, 5));
    }

    [Fact]
    public async Task RefundCreditsAsync_CallsAddCredits()
    {
        // Arrange
        var userId = "test-user";
        var user = TestDataBuilder.CreateUser(id: userId, credits: 10);
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(user);

        // Act
        var result = await _creditService.RefundCreditsAsync(userId, 3);

        // Assert
        Assert.True(result);
        Assert.Equal(13, user.Credits);
    }
}

