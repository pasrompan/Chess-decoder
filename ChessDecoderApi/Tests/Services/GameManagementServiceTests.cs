using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Interfaces;
using ChessDecoderApi.Services;
using ChessDecoderApi.Services.GameProcessing;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services;

public class GameManagementServiceTests
{
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IChessGameRepository> _gameRepositoryMock;
    private readonly Mock<IGameImageRepository> _imageRepositoryMock;
    private readonly Mock<IGameStatisticsRepository> _statsRepositoryMock;
    private readonly Mock<ILogger<GameManagementService>> _loggerMock;
    private readonly GameManagementService _service;

    public GameManagementServiceTests()
    {
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());
        _gameRepositoryMock = new Mock<IChessGameRepository>();
        _imageRepositoryMock = new Mock<IGameImageRepository>();
        _statsRepositoryMock = new Mock<IGameStatisticsRepository>();
        _loggerMock = new Mock<ILogger<GameManagementService>>();

        _repositoryFactoryMock.Setup(x => x.CreateChessGameRepositoryAsync()).ReturnsAsync(_gameRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameImageRepositoryAsync()).ReturnsAsync(_imageRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameStatisticsRepositoryAsync()).ReturnsAsync(_statsRepositoryMock.Object);

        _service = new GameManagementService(_repositoryFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetGameByIdAsync_ExistingGame_ReturnsGameDetails()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync(new List<GameImage>());
        _statsRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync((GameStatistics?)null);

        // Act
        var result = await _service.GetGameByIdAsync(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal(game.UserId, result.UserId);
    }

    [Fact]
    public async Task GetGameByIdAsync_NonExistingGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync((ChessGame?)null);

        // Act
        var result = await _service.GetGameByIdAsync(gameId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserGamesAsync_ReturnsPagedGames()
    {
        // Arrange
        var userId = "test-user";
        var games = TestDataBuilder.CreateChessGames(3, userId);
        _gameRepositoryMock.Setup(x => x.GetByUserIdPaginatedAsync(userId, 1, 10))
            .ReturnsAsync((games, 3));
        _statsRepositoryMock.Setup(x => x.GetByChessGameIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((GameStatistics?)null);

        // Act
        var result = await _service.GetUserGamesAsync(userId, 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Games.Count);
    }

    [Fact]
    public async Task DeleteGameAsync_ExistingGame_DeletesAndReturnsTrue()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _imageRepositoryMock.Setup(x => x.DeleteByChessGameIdAsync(gameId)).ReturnsAsync(true);
        _statsRepositoryMock.Setup(x => x.DeleteByChessGameIdAsync(gameId)).ReturnsAsync(true);
        _gameRepositoryMock.Setup(x => x.DeleteAsync(gameId)).ReturnsAsync(true);

        // Act
        var result = await _service.DeleteGameAsync(gameId);

        // Assert
        Assert.True(result);
        _gameRepositoryMock.Verify(x => x.DeleteAsync(gameId), Times.Once);
    }

    [Fact]
    public async Task DeleteGameAsync_NonExistingGame_ReturnsFalse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _imageRepositoryMock.Setup(x => x.DeleteByChessGameIdAsync(gameId)).ReturnsAsync(false);
        _statsRepositoryMock.Setup(x => x.DeleteByChessGameIdAsync(gameId)).ReturnsAsync(false);
        _gameRepositoryMock.Setup(x => x.DeleteAsync(gameId)).ReturnsAsync(false);

        // Act
        var result = await _service.DeleteGameAsync(gameId);

        // Assert
        Assert.False(result);
    }
}

