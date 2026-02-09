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
    private readonly Mock<IImageProcessingService> _imageProcessingServiceMock;
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
        _imageProcessingServiceMock = new Mock<IImageProcessingService>();
        _loggerMock = new Mock<ILogger<GameManagementService>>();

        _repositoryFactoryMock.Setup(x => x.CreateChessGameRepositoryAsync()).ReturnsAsync(_gameRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameImageRepositoryAsync()).ReturnsAsync(_imageRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameStatisticsRepositoryAsync()).ReturnsAsync(_statsRepositoryMock.Object);

        _service = new GameManagementService(_repositoryFactoryMock.Object, _imageProcessingServiceMock.Object, _loggerMock.Object);
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

    [Fact]
    public async Task UpdatePgnContentAsync_ExistingGameWithCorrectUser_UpdatesPgnAndReturnsDetails()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        var newPgnContent = "1. d4 d5 2. c4 e6";
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _gameRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync(new List<GameImage>());
        _statsRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync((GameStatistics?)null);

        // Act
        var result = await _service.UpdatePgnContentAsync(game.Id, game.UserId, newPgnContent);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newPgnContent, result.PgnContent);
        Assert.Equal(1, result.EditCount);
        Assert.NotNull(result.LastEditedAt);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.Is<ChessGame>(g => 
            g.PgnContent == newPgnContent && 
            g.EditCount == 1 &&
            g.LastEditedAt != null)), Times.Once);
    }

    [Fact]
    public async Task UpdatePgnContentAsync_NonExistingGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var pgnContent = "1. e4 e5";
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync((ChessGame?)null);

        // Act
        var result = await _service.UpdatePgnContentAsync(gameId, userId, pgnContent);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdatePgnContentAsync_WrongUser_ReturnsNull()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        var wrongUserId = "wrong-user";
        var pgnContent = "1. e4 e5";
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);

        // Act
        var result = await _service.UpdatePgnContentAsync(game.Id, wrongUserId, pgnContent);

        // Assert
        Assert.Null(result);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ChessGame>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePgnContentAsync_EmptyPgn_ThrowsArgumentException()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.UpdatePgnContentAsync(game.Id, game.UserId, ""));
    }

    [Fact]
    public async Task MarkProcessingCompleteAsync_ExistingGameWithCorrectUser_SetsFlag()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        game.ProcessingCompleted = false;
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _gameRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync(new List<GameImage>());
        _statsRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync((GameStatistics?)null);

        // Act
        var result = await _service.MarkProcessingCompleteAsync(game.Id, game.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ProcessingCompleted);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.Is<ChessGame>(g => g.ProcessingCompleted)), Times.Once);
    }

    [Fact]
    public async Task MarkProcessingCompleteAsync_AlreadyCompleted_IsIdempotent()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        game.ProcessingCompleted = true; // Already completed
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync(new List<GameImage>());
        _statsRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync((GameStatistics?)null);

        // Act
        var result = await _service.MarkProcessingCompleteAsync(game.Id, game.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ProcessingCompleted);
        // Should NOT call Update since already completed
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ChessGame>()), Times.Never);
    }

    [Fact]
    public async Task MarkProcessingCompleteAsync_NonExistingGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync((ChessGame?)null);

        // Act
        var result = await _service.MarkProcessingCompleteAsync(gameId, userId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MarkProcessingCompleteAsync_WrongUser_ReturnsNull()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        var wrongUserId = "wrong-user";
        
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);

        // Act
        var result = await _service.MarkProcessingCompleteAsync(game.Id, wrongUserId);

        // Assert
        Assert.Null(result);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ChessGame>()), Times.Never);
    }
}

