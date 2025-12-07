using ChessDecoderApi.DTOs;
using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Interfaces;
using ChessDecoderApi.Services;
using ChessDecoderApi.Services.GameProcessing;
using ChessDecoderApi.Services.ImageProcessing;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services;

public class GameProcessingMetadataTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ICreditService> _creditServiceMock;
    private readonly Mock<ICloudStorageService> _cloudStorageServiceMock;
    private readonly Mock<IImageExtractionService> _imageExtractionServiceMock;
    private readonly Mock<IImageManipulationService> _imageManipulationServiceMock;
    private readonly Mock<IImageProcessingService> _legacyImageProcessingServiceMock;
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IChessGameRepository> _gameRepositoryMock;
    private readonly Mock<IGameImageRepository> _imageRepositoryMock;
    private readonly Mock<IGameStatisticsRepository> _statsRepositoryMock;
    private readonly Mock<ILogger<GameProcessingService>> _loggerMock;
    private readonly GameProcessingService _service;

    public GameProcessingMetadataTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _creditServiceMock = new Mock<ICreditService>();
        _cloudStorageServiceMock = new Mock<ICloudStorageService>();
        _imageExtractionServiceMock = new Mock<IImageExtractionService>();
        _imageManipulationServiceMock = new Mock<IImageManipulationService>();
        _legacyImageProcessingServiceMock = new Mock<IImageProcessingService>();
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());
        _gameRepositoryMock = new Mock<IChessGameRepository>();
        _imageRepositoryMock = new Mock<IGameImageRepository>();
        _statsRepositoryMock = new Mock<IGameStatisticsRepository>();
        _loggerMock = new Mock<ILogger<GameProcessingService>>();

        _repositoryFactoryMock
            .Setup(x => x.CreateChessGameRepositoryAsync())
            .ReturnsAsync(_gameRepositoryMock.Object);
        _repositoryFactoryMock
            .Setup(x => x.CreateGameImageRepositoryAsync())
            .ReturnsAsync(_imageRepositoryMock.Object);
        _repositoryFactoryMock
            .Setup(x => x.CreateGameStatisticsRepositoryAsync())
            .ReturnsAsync(_statsRepositoryMock.Object);

        _service = new GameProcessingService(
            _authServiceMock.Object,
            _creditServiceMock.Object,
            _cloudStorageServiceMock.Object,
            _imageExtractionServiceMock.Object,
            _imageManipulationServiceMock.Object,
            _legacyImageProcessingServiceMock.Object,
            _repositoryFactoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessGameUploadAsync_WithMetadata_ShouldStoreMetadataInGame()
    {
        // Arrange
        var userId = "test-user";
        var user = new User { Id = userId, Email = "test@example.com" };
        var request = CreateGameUploadRequestWithMetadata();

        _authServiceMock.Setup(x => x.GetUserProfileAsync(userId)).ReturnsAsync(user);
        _creditServiceMock.Setup(x => x.HasEnoughCreditsAsync(userId, 1)).ReturnsAsync(true);
        _creditServiceMock.Setup(x => x.GetUserCreditsAsync(userId)).ReturnsAsync(100);
        _creditServiceMock.Setup(x => x.DeductCreditsAsync(userId, 1)).ReturnsAsync(true);

        _cloudStorageServiceMock
            .Setup(x => x.UploadGameImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Cloud storage unavailable"));

        var chessGameResponse = new ChessGameResponse
        {
            PgnContent = "[Date \"2025.12.07\"]\n[White \"John Doe\"]\n[Black \"Jane Smith\"]\n\n1. e4 e5 *",
            Validation = new ChessGameValidation
            {
                GameId = Guid.NewGuid().ToString(),
                Moves = new List<ChessMovePair>()
            }
        };

        _imageExtractionServiceMock
            .Setup(x => x.ProcessImageAsync(It.IsAny<string>(), It.IsAny<PgnMetadata>()))
            .ReturnsAsync(chessGameResponse);

        _gameRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameImage>())).ReturnsAsync((GameImage img) => img);
        _statsRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameStatistics>())).ReturnsAsync((GameStatistics s) => s);

        // Act
        var result = await _service.ProcessGameUploadAsync(request);

        // Assert
        _gameRepositoryMock.Verify(x => x.CreateAsync(It.Is<ChessGame>(g =>
            g.WhitePlayer == "John Doe" &&
            g.BlackPlayer == "Jane Smith" &&
            g.GameDate.HasValue &&
            g.GameDate.Value.Date == new DateTime(2025, 12, 7).Date &&
            g.Round == "1"
        )), Times.Once);
    }

    [Fact]
    public async Task ProcessGameUploadAsync_WithPartialMetadata_ShouldStoreOnlyProvidedFields()
    {
        // Arrange
        var userId = "test-user";
        var user = new User { Id = userId, Email = "test@example.com" };
        var request = CreateGameUploadRequestWithPartialMetadata();

        _authServiceMock.Setup(x => x.GetUserProfileAsync(userId)).ReturnsAsync(user);
        _creditServiceMock.Setup(x => x.HasEnoughCreditsAsync(userId, 1)).ReturnsAsync(true);
        _creditServiceMock.Setup(x => x.GetUserCreditsAsync(userId)).ReturnsAsync(100);
        _creditServiceMock.Setup(x => x.DeductCreditsAsync(userId, 1)).ReturnsAsync(true);

        _cloudStorageServiceMock
            .Setup(x => x.UploadGameImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Cloud storage unavailable"));

        var chessGameResponse = new ChessGameResponse
        {
            PgnContent = "[Date \"2025.12.07\"]\n[White \"John Doe\"]\n[Black \"?\"]\n\n1. e4 e5 *",
            Validation = new ChessGameValidation
            {
                GameId = Guid.NewGuid().ToString(),
                Moves = new List<ChessMovePair>()
            }
        };

        _imageExtractionServiceMock
            .Setup(x => x.ProcessImageAsync(It.IsAny<string>(), It.IsAny<PgnMetadata>()))
            .ReturnsAsync(chessGameResponse);

        _gameRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameImage>())).ReturnsAsync((GameImage img) => img);
        _statsRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameStatistics>())).ReturnsAsync((GameStatistics s) => s);

        // Act
        var result = await _service.ProcessGameUploadAsync(request);

        // Assert
        _gameRepositoryMock.Verify(x => x.CreateAsync(It.Is<ChessGame>(g =>
            g.WhitePlayer == "John Doe" &&
            g.BlackPlayer == null &&
            g.GameDate.HasValue &&
            g.Round == null
        )), Times.Once);
    }

    [Fact]
    public async Task ProcessGameUploadAsync_WithoutMetadata_ShouldStoreNullValues()
    {
        // Arrange
        var userId = "test-user";
        var user = new User { Id = userId, Email = "test@example.com" };
        var request = CreateGameUploadRequestWithoutMetadata();

        _authServiceMock.Setup(x => x.GetUserProfileAsync(userId)).ReturnsAsync(user);
        _creditServiceMock.Setup(x => x.HasEnoughCreditsAsync(userId, 1)).ReturnsAsync(true);
        _creditServiceMock.Setup(x => x.GetUserCreditsAsync(userId)).ReturnsAsync(100);
        _creditServiceMock.Setup(x => x.DeductCreditsAsync(userId, 1)).ReturnsAsync(true);

        _cloudStorageServiceMock
            .Setup(x => x.UploadGameImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Cloud storage unavailable"));

        var chessGameResponse = new ChessGameResponse
        {
            PgnContent = "[Date \"????.??.??\"]\n[White \"?\"]\n[Black \"?\"]\n\n1. e4 e5 *",
            Validation = new ChessGameValidation
            {
                GameId = Guid.NewGuid().ToString(),
                Moves = new List<ChessMovePair>()
            }
        };

        _imageExtractionServiceMock
            .Setup(x => x.ProcessImageAsync(It.IsAny<string>(), It.IsAny<PgnMetadata>()))
            .ReturnsAsync(chessGameResponse);

        _gameRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameImage>())).ReturnsAsync((GameImage img) => img);
        _statsRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameStatistics>())).ReturnsAsync((GameStatistics s) => s);

        // Act
        var result = await _service.ProcessGameUploadAsync(request);

        // Assert
        _gameRepositoryMock.Verify(x => x.CreateAsync(It.Is<ChessGame>(g =>
            g.WhitePlayer == null &&
            g.BlackPlayer == null &&
            g.GameDate == null &&
            g.Round == null
        )), Times.Once);
    }

    [Fact]
    public async Task ProcessGameUploadAsync_WithMetadata_ShouldPassMetadataToImageExtraction()
    {
        // Arrange
        var userId = "test-user";
        var user = new User { Id = userId, Email = "test@example.com" };
        var request = CreateGameUploadRequestWithMetadata();

        _authServiceMock.Setup(x => x.GetUserProfileAsync(userId)).ReturnsAsync(user);
        _creditServiceMock.Setup(x => x.HasEnoughCreditsAsync(userId, 1)).ReturnsAsync(true);
        _creditServiceMock.Setup(x => x.GetUserCreditsAsync(userId)).ReturnsAsync(100);
        _creditServiceMock.Setup(x => x.DeductCreditsAsync(userId, 1)).ReturnsAsync(true);

        _cloudStorageServiceMock
            .Setup(x => x.UploadGameImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Cloud storage unavailable"));

        var chessGameResponse = new ChessGameResponse
        {
            PgnContent = "[Date \"2025.12.07\"]\n[White \"John Doe\"]\n[Black \"Jane Smith\"]\n\n1. e4 e5 *",
            Validation = new ChessGameValidation
            {
                GameId = Guid.NewGuid().ToString(),
                Moves = new List<ChessMovePair>()
            }
        };

        _imageExtractionServiceMock
            .Setup(x => x.ProcessImageAsync(It.IsAny<string>(), It.IsAny<PgnMetadata>()))
            .ReturnsAsync(chessGameResponse);

        _gameRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameImage>())).ReturnsAsync((GameImage img) => img);
        _statsRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameStatistics>())).ReturnsAsync((GameStatistics s) => s);

        // Act
        await _service.ProcessGameUploadAsync(request);

        // Assert
        _imageExtractionServiceMock.Verify(x => x.ProcessImageAsync(
            It.IsAny<string>(),
            It.Is<PgnMetadata>(m =>
                m.WhitePlayer == "John Doe" &&
                m.BlackPlayer == "Jane Smith" &&
                m.GameDate.HasValue &&
                m.GameDate.Value.Date == new DateTime(2025, 12, 7).Date &&
                m.Round == "1"
            )
        ), Times.Once);
    }

    private GameUploadRequest CreateGameUploadRequestWithMetadata()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken ct) =>
            {
                stream.Write(new byte[1024], 0, 1024);
                return Task.CompletedTask;
            });

        return new GameUploadRequest
        {
            Image = fileMock.Object,
            UserId = "test-user",
            AutoCrop = false,
            WhitePlayer = "John Doe",
            BlackPlayer = "Jane Smith",
            GameDate = new DateTime(2025, 12, 7),
            Round = "1"
        };
    }

    private GameUploadRequest CreateGameUploadRequestWithPartialMetadata()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken ct) =>
            {
                stream.Write(new byte[1024], 0, 1024);
                return Task.CompletedTask;
            });

        return new GameUploadRequest
        {
            Image = fileMock.Object,
            UserId = "test-user",
            AutoCrop = false,
            WhitePlayer = "John Doe",
            BlackPlayer = null,
            GameDate = new DateTime(2025, 12, 7),
            Round = null
        };
    }

    private GameUploadRequest CreateGameUploadRequestWithoutMetadata()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken ct) =>
            {
                stream.Write(new byte[1024], 0, 1024);
                return Task.CompletedTask;
            });

        return new GameUploadRequest
        {
            Image = fileMock.Object,
            UserId = "test-user",
            AutoCrop = false,
            WhitePlayer = null,
            BlackPlayer = null,
            GameDate = null,
            Round = null
        };
    }
}

