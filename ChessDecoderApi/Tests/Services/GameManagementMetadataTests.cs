using ChessDecoderApi.DTOs;
using ChessDecoderApi.DTOs.Requests;
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

public class GameManagementMetadataTests
{
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IChessGameRepository> _gameRepositoryMock;
    private readonly Mock<IImageProcessingService> _imageProcessingServiceMock;
    private readonly Mock<IProjectService> _projectServiceMock;
    private readonly Mock<ICloudStorageService> _cloudStorageServiceMock;
    private readonly Mock<ILogger<GameManagementService>> _loggerMock;
    private readonly GameManagementService _service;

    public GameManagementMetadataTests()
    {
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());
        _gameRepositoryMock = new Mock<IChessGameRepository>();
        _imageProcessingServiceMock = new Mock<IImageProcessingService>();
        _projectServiceMock = new Mock<IProjectService>();
        _cloudStorageServiceMock = new Mock<ICloudStorageService>();
        _loggerMock = new Mock<ILogger<GameManagementService>>();

        _repositoryFactoryMock.Setup(x => x.CreateChessGameRepositoryAsync()).ReturnsAsync(_gameRepositoryMock.Object);

        _service = new GameManagementService(
            _repositoryFactoryMock.Object,
            _imageProcessingServiceMock.Object,
            _projectServiceMock.Object,
            _cloudStorageServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UpdateGameMetadataAsync_ExistingGame_ShouldUpdateMetadataAndRegeneratePGN()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = TestDataBuilder.CreateChessGame();
        existingGame.Id = gameId;
        existingGame.PgnContent = "[Date \"????.??.??\"]\n[White \"?\"]\n[Black \"?\"]\n\n1. e4 e5 *";

        var request = new UpdateGameMetadataRequest
        {
            WhitePlayer = "John Doe",
            BlackPlayer = "Jane Smith",
            GameDate = new DateTime(2025, 12, 7),
            Round = "1"
        };

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(existingGame);
        _gameRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageProcessingServiceMock
            .Setup(x => x.GeneratePGNContentAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<PgnMetadata>()))
            .Returns("[Date \"2025.12.07\"]\n[Round \"1\"]\n[White \"John Doe\"]\n[Black \"Jane Smith\"]\n\n1. e4 e5 *");

        // Act
        var result = await _service.UpdateGameMetadataAsync(gameId, request);

        // Assert
        Assert.True(result);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.Is<ChessGame>(g =>
            g.WhitePlayer == "John Doe" &&
            g.BlackPlayer == "Jane Smith" &&
            g.GameDate.HasValue &&
            g.GameDate.Value.Date == new DateTime(2025, 12, 7).Date &&
            g.Round == "1" &&
            g.PgnContent.Contains("John Doe") &&
            g.PgnContent.Contains("Jane Smith")
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateGameMetadataAsync_NonExistingGame_ShouldReturnFalse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new UpdateGameMetadataRequest
        {
            WhitePlayer = "John Doe"
        };

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync((ChessGame?)null);

        // Act
        var result = await _service.UpdateGameMetadataAsync(gameId, request);

        // Assert
        Assert.False(result);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ChessGame>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGameMetadataAsync_ShouldExtractMovesFromExistingPGN()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = TestDataBuilder.CreateChessGame();
        existingGame.Id = gameId;
        existingGame.PgnContent = "[Date \"????.??.??\"]\n[White \"?\"]\n[Black \"?\"]\n\n1. e4 e5 2. Nf3 Nc6 *";

        var request = new UpdateGameMetadataRequest
        {
            WhitePlayer = "John Doe"
        };

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(existingGame);
        _gameRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageProcessingServiceMock
            .Setup(x => x.GeneratePGNContentAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<PgnMetadata>()))
            .Returns("[Date \"????.??.??\"]\n[White \"John Doe\"]\n[Black \"?\"]\n\n1. e4 e5 2. Nf3 Nc6 *");

        // Act
        await _service.UpdateGameMetadataAsync(gameId, request);

        // Assert
        _imageProcessingServiceMock.Verify(x => x.GeneratePGNContentAsync(
            It.Is<IEnumerable<string>>(w => w.SequenceEqual(new[] { "e4", "Nf3" })),
            It.Is<IEnumerable<string>>(b => b.SequenceEqual(new[] { "e5", "Nc6" })),
            It.IsAny<PgnMetadata>()
        ), Times.Once);
    }

    [Fact]
    public async Task UpdateGameMetadataAsync_PartialUpdate_ShouldOnlyUpdateProvidedFields()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = TestDataBuilder.CreateChessGame();
        existingGame.Id = gameId;
        existingGame.WhitePlayer = "Original White";
        existingGame.BlackPlayer = "Original Black";
        existingGame.GameDate = new DateTime(2024, 1, 1);
        existingGame.Round = "Original Round";
        existingGame.PgnContent = "[Date \"2024.01.01\"]\n[Round \"Original Round\"]\n[White \"Original White\"]\n[Black \"Original Black\"]\n\n1. e4 e5 *";

        var request = new UpdateGameMetadataRequest
        {
            WhitePlayer = "Updated White"
            // Only updating WhitePlayer, others should remain unchanged
        };

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(existingGame);
        _gameRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageProcessingServiceMock
            .Setup(x => x.GeneratePGNContentAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<PgnMetadata>()))
            .Returns("[Date \"2024.01.01\"]\n[Round \"Original Round\"]\n[White \"Updated White\"]\n[Black \"Original Black\"]\n\n1. e4 e5 *");

        // Act
        await _service.UpdateGameMetadataAsync(gameId, request);

        // Assert
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.Is<ChessGame>(g =>
            g.WhitePlayer == "Updated White" &&
            g.BlackPlayer == "Original Black" &&
            g.GameDate.HasValue &&
            g.GameDate.Value.Date == new DateTime(2024, 1, 1).Date &&
            g.Round == "Original Round"
        )), Times.Once);
    }

    [Fact]
    public async Task GetGameByIdAsync_WithMetadata_ShouldReturnMetadataInResponse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = TestDataBuilder.CreateChessGame();
        game.Id = gameId;
        game.WhitePlayer = "John Doe";
        game.BlackPlayer = "Jane Smith";
        game.GameDate = new DateTime(2025, 12, 7);
        game.Round = "1";

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(game);
        _gameRepositoryMock.Setup(x => x.GetByUserIdPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<ChessGame>(), 0));

        var imageRepoMock = new Mock<IGameImageRepository>();
        var statsRepoMock = new Mock<IGameStatisticsRepository>();
        _repositoryFactoryMock.Setup(x => x.CreateGameImageRepositoryAsync()).ReturnsAsync(imageRepoMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameStatisticsRepositoryAsync()).ReturnsAsync(statsRepoMock.Object);
        imageRepoMock.Setup(x => x.GetByChessGameIdAsync(gameId)).ReturnsAsync(new List<GameImage>());
        statsRepoMock.Setup(x => x.GetByChessGameIdAsync(gameId)).ReturnsAsync((GameStatistics?)null);

        // Act
        var result = await _service.GetGameByIdAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John Doe", result.WhitePlayer);
        Assert.Equal("Jane Smith", result.BlackPlayer);
        Assert.Equal(new DateTime(2025, 12, 7), result.GameDate);
        Assert.Equal("1", result.Round);
    }
}
