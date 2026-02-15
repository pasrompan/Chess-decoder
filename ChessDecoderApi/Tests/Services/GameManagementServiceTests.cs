using ChessDecoderApi.Models;
using ChessDecoderApi.DTOs;
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
    private readonly Mock<IProjectService> _projectServiceMock;
    private readonly Mock<ICloudStorageService> _cloudStorageServiceMock;
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
        _projectServiceMock = new Mock<IProjectService>();
        _cloudStorageServiceMock = new Mock<ICloudStorageService>();
        _loggerMock = new Mock<ILogger<GameManagementService>>();

        _repositoryFactoryMock.Setup(x => x.CreateChessGameRepositoryAsync()).ReturnsAsync(_gameRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameImageRepositoryAsync()).ReturnsAsync(_imageRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameStatisticsRepositoryAsync()).ReturnsAsync(_statsRepositoryMock.Object);

        _service = new GameManagementService(
            _repositoryFactoryMock.Object,
            _imageProcessingServiceMock.Object,
            _projectServiceMock.Object,
            _cloudStorageServiceMock.Object,
            _loggerMock.Object);
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
    public async Task DeleteGameAsync_WhenRepositoryThrows_ReturnsFalse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(x => x.DeleteAsync(gameId)).ThrowsAsync(new Exception("db failure"));

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
    public async Task UpdatePgnContentAsync_CorruptedPgn_ShouldNormalizeBeforeSave()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        var corruptedPgn = @"[Event ""ChessScribe Game""]
[Site ""ChessScribe""]
[Date ""2026.02.15""]
[White ""?""]
[Black ""?""]
[Result ""*""]

1. [Date ""????.??.??""] 2. [White ""?""] 3. [Black ""?""] 4. [Result ""*""] 5. d4 Nf6 6. Nf3 g6 *";

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _gameRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync(new List<GameImage>());
        _statsRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync((GameStatistics?)null);
        _imageProcessingServiceMock
            .Setup(x => x.GeneratePGNContentAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<PgnMetadata>()))
            .Returns("[Date \"2026.02.15\"]\n[White \"?\"]\n[Black \"?\"]\n[Result \"*\"]\n\n1. d4 Nf6 2. Nf3 g6 *");

        // Act
        var result = await _service.UpdatePgnContentAsync(game.Id, game.UserId, corruptedPgn);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("1. [Date", result.PgnContent);
        Assert.Contains("1. d4 Nf6", result.PgnContent);
        Assert.Contains("2. Nf3 g6", result.PgnContent);
        Assert.Contains("[Event \"ChessScribe Game\"]", result.PgnContent);
        Assert.Contains("[Site \"ChessScribe\"]", result.PgnContent);
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
    public async Task UpdatePgnContentAsync_CorruptedPgnWithoutMoveData_ThrowsArgumentException()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        var invalidCorruptedPgn = @"[Event ""ChessScribe Game""]
[Site ""ChessScribe""]
[Date ""2026.02.15""]
[White ""?""]
[Black ""?""]
[Result ""*""]

this is [broken] notation";

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdatePgnContentAsync(game.Id, game.UserId, invalidCorruptedPgn));
        Assert.Contains("valid move data", ex.Message);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ChessGame>()), Times.Never);
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

    [Fact]
    public async Task GetGameByIdAsync_ImageVariantMissing_DefaultsToOriginal()
    {
        // Arrange
        var game = TestDataBuilder.CreateChessGame();
        _gameRepositoryMock.Setup(x => x.GetByIdAsync(game.Id)).ReturnsAsync(game);
        _statsRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync((GameStatistics?)null);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(game.Id)).ReturnsAsync(new List<GameImage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChessGameId = game.Id,
                FileName = "board.jpg",
                FilePath = "/tmp/board.jpg",
                Variant = ""
            }
        });

        // Act
        var result = await _service.GetGameByIdAsync(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Images);
        Assert.Equal("original", result.Images[0].Variant);
    }

    [Fact]
    public async Task GetGameImageAsync_LocalProcessedVariant_ReturnsSelectedVariantStream()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var game = TestDataBuilder.CreateChessGame(id: gameId, userId: userId);
        var tempFile = Path.GetTempFileName();
        var content = "processed-image-content";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(game);
            _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(gameId)).ReturnsAsync(new List<GameImage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ChessGameId = gameId,
                    FileName = "processed.jpg",
                    FilePath = tempFile,
                    FileType = "image/jpeg",
                    Variant = "processed",
                    IsStoredInCloud = false
                }
            });

            // Act
            var result = await _service.GetGameImageAsync(gameId, userId, "processed");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("image/jpeg", result.ContentType);
            Assert.Equal("processed", result.Variant);
            using var reader = new StreamReader(result.Stream);
            var streamText = await reader.ReadToEndAsync();
            Assert.Equal(content, streamText);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetGameImageAsync_MissingRequestedVariant_FallsBackToOriginal()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var game = TestDataBuilder.CreateChessGame(id: gameId, userId: userId);
        var tempFile = Path.GetTempFileName();
        var content = "original-image-content";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(game);
            _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(gameId)).ReturnsAsync(new List<GameImage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ChessGameId = gameId,
                    FileName = "original.jpg",
                    FilePath = tempFile,
                    FileType = "image/png",
                    Variant = "original",
                    IsStoredInCloud = false
                }
            });

            // Act
            var result = await _service.GetGameImageAsync(gameId, userId, "processed");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("original", result.Variant);
            Assert.Equal("image/png", result.ContentType);
            using var reader = new StreamReader(result.Stream);
            var streamText = await reader.ReadToEndAsync();
            Assert.Equal(content, streamText);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetGameImageAsync_CloudImage_ReturnsDownloadedStream()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var game = TestDataBuilder.CreateChessGame(id: gameId, userId: userId);
        var cloudStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(game);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(gameId)).ReturnsAsync(new List<GameImage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChessGameId = gameId,
                FileName = "processed.jpg",
                FileType = "image/jpeg",
                Variant = "processed",
                IsStoredInCloud = true,
                CloudStorageObjectName = "games/processed.jpg"
            }
        });
        _cloudStorageServiceMock
            .Setup(x => x.DownloadGameImageAsync("games/processed.jpg"))
            .ReturnsAsync(cloudStream);

        // Act
        var result = await _service.GetGameImageAsync(gameId, userId, "processed");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cloudStream, result.Stream);
        Assert.Equal("processed", result.Variant);
        _cloudStorageServiceMock.Verify(x => x.DownloadGameImageAsync("games/processed.jpg"), Times.Once);
    }

    [Fact]
    public async Task GetGameImageAsync_CloudImageWithoutObjectName_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var game = TestDataBuilder.CreateChessGame(id: gameId, userId: userId);

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(game);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(gameId)).ReturnsAsync(new List<GameImage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChessGameId = gameId,
                FileName = "processed.jpg",
                FileType = "image/jpeg",
                Variant = "processed",
                IsStoredInCloud = true,
                CloudStorageObjectName = null
            }
        });

        // Act
        var result = await _service.GetGameImageAsync(gameId, userId, "processed");

        // Assert
        Assert.Null(result);
        _cloudStorageServiceMock.Verify(x => x.DownloadGameImageAsync(It.IsAny<string>()), Times.Never);
    }
}
