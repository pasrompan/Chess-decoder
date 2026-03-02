using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Interfaces;
using ChessDecoderApi.Services;
using ChessDecoderApi.Services.GameProcessing;
using ChessDecoderApi.Services.ImageProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services;

public class GameContinuationServiceTests
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<ICreditService> _creditServiceMock = new();
    private readonly Mock<ICloudStorageService> _cloudStorageServiceMock = new();
    private readonly Mock<IImageExtractionService> _imageExtractionServiceMock = new();
    private readonly Mock<IImageManipulationService> _imageManipulationServiceMock = new();
    private readonly Mock<IImageProcessingService> _legacyImageProcessingServiceMock = new();
    private readonly Mock<IProjectService> _projectServiceMock = new();
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IChessGameRepository> _gameRepositoryMock = new();
    private readonly Mock<IGameImageRepository> _imageRepositoryMock = new();
    private readonly Mock<IGameStatisticsRepository> _statsRepositoryMock = new();
    private readonly GameProcessingService _service;

    public GameContinuationServiceTests()
    {
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());

        _repositoryFactoryMock.Setup(x => x.CreateChessGameRepositoryAsync()).ReturnsAsync(_gameRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameImageRepositoryAsync()).ReturnsAsync(_imageRepositoryMock.Object);
        _repositoryFactoryMock.Setup(x => x.CreateGameStatisticsRepositoryAsync()).ReturnsAsync(_statsRepositoryMock.Object);

        _gameRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _gameRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ChessGame>())).ReturnsAsync((ChessGame g) => g);
        _imageRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameImage>())).ReturnsAsync((GameImage i) => i);
        _imageRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<GameImage>())).ReturnsAsync((GameImage i) => i);
        _statsRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<GameStatistics>())).ReturnsAsync((GameStatistics s) => s);
        _statsRepositoryMock.Setup(x => x.CreateOrUpdateAsync(It.IsAny<GameStatistics>())).ReturnsAsync((GameStatistics s) => s);

        _service = new GameProcessingService(
            _authServiceMock.Object,
            _creditServiceMock.Object,
            _cloudStorageServiceMock.Object,
            _imageExtractionServiceMock.Object,
            _imageManipulationServiceMock.Object,
            _legacyImageProcessingServiceMock.Object,
            _projectServiceMock.Object,
            _repositoryFactoryMock.Object,
            Mock.Of<ILogger<GameProcessingService>>());
    }

    [Fact]
    public async Task ProcessDualGameUploadAsync_DefaultsSecondImageToContinuationNumbering()
    {
        var userId = "test-user";
        _authServiceMock.Setup(x => x.GetUserProfileAsync(userId)).ReturnsAsync(new User { Id = userId, Email = "u@test.com" });
        _creditServiceMock.Setup(x => x.HasEnoughCreditsAsync(userId, 1)).ReturnsAsync(true);
        _creditServiceMock.Setup(x => x.DeductCreditsAsync(userId, 1)).ReturnsAsync(true);
        _creditServiceMock.Setup(x => x.GetUserCreditsAsync(userId)).ReturnsAsync(9);

        _cloudStorageServiceMock
            .SetupSequence(x => x.UploadGameImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("obj/page-a.jpg")
            .ReturnsAsync("obj/page-b.jpg");
        _cloudStorageServiceMock
            .Setup(x => x.GetImageUrlAsync(It.IsAny<string>()))
            .ReturnsAsync((string objectName) => $"https://example.test/{objectName}");

        _imageExtractionServiceMock
            .Setup(x => x.ProcessImageAsync(It.IsAny<string>(), It.IsAny<ChessDecoderApi.DTOs.PgnMetadata>()))
            .ReturnsAsync(new ChessGameResponse
            {
                Validation = new ChessGameValidation
                {
                    Moves = new List<ChessMovePair>
                    {
                        new() { MoveNumber = 1, WhiteMove = CreateMove("e4"), BlackMove = CreateMove("e5") },
                        new() { MoveNumber = 2, WhiteMove = CreateMove("Nf3"), BlackMove = CreateMove("Nc6") }
                    }
                }
            });

        _imageExtractionServiceMock
            .Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>()))
            .ReturnsAsync((
                new List<string> { "Bb5", "Ba4" },
                new List<string> { "a6", "Nf6" }));

        var request = new DualGameUploadRequest
        {
            UserId = userId,
            Page1 = CreateImageFile("first-page.jpg"),
            Page2 = CreateImageFile("second-page.jpg"),
            AutoCrop = false
        };

        var result = await _service.ProcessDualGameUploadAsync(request);

        Assert.Equal(1, result.Page1.PageNumber);
        Assert.Equal(2, result.Page2.PageNumber);
        Assert.Equal(1, result.Page1.StartingMoveNumber);
        Assert.Equal(3, result.Page2.StartingMoveNumber);
        Assert.Contains("1. e4 e5", result.MergedPgn);
        Assert.Contains("3. Bb5 a6", result.MergedPgn);
        Assert.Contains("normalized", result.ContinuationValidation.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddContinuationAsync_NormalizesMoveNumbersFromPageOneEnd()
    {
        var userId = "test-user";
        var gameId = Guid.NewGuid();
        var game = new ChessGame
        {
            Id = gameId,
            UserId = userId,
            PgnContent = "[Date \"2026.02.28\"]\n[White \"?\"]\n[Black \"?\"]\n[Result \"*\"]\n\n1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 *",
            HasContinuation = false,
            WhitePlayer = "?",
            BlackPlayer = "?"
        };

        _authServiceMock.Setup(x => x.GetUserProfileAsync(userId)).ReturnsAsync(new User { Id = userId, Email = "u@test.com" });
        _creditServiceMock.Setup(x => x.HasEnoughCreditsAsync(userId, 1)).ReturnsAsync(true);
        _creditServiceMock.Setup(x => x.DeductCreditsAsync(userId, 1)).ReturnsAsync(true);

        _gameRepositoryMock.Setup(x => x.GetByIdAsync(gameId)).ReturnsAsync(game);
        _imageRepositoryMock.Setup(x => x.GetByChessGameIdAsync(gameId)).ReturnsAsync(new List<GameImage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChessGameId = gameId,
                FileName = "page1.jpg",
                FilePath = "/tmp/page1.jpg",
                Variant = "original",
                PageNumber = 1,
                StartingMoveNumber = 1,
                EndingMoveNumber = 4
            }
        });

        _cloudStorageServiceMock
            .Setup(x => x.UploadGameImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("obj/cont.jpg");
        _cloudStorageServiceMock
            .Setup(x => x.GetImageUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://example.test/obj/cont.jpg");

        _imageExtractionServiceMock
            .Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>()))
            .ReturnsAsync((
                new List<string> { "O-O", "Re1" },
                new List<string> { "Be7", "b5" }
            ));

        var response = await _service.AddContinuationAsync(gameId, new ContinuationUploadRequest
        {
            UserId = userId,
            Image = CreateImageFile("cont.jpg"),
            AutoCrop = false
        });

        Assert.False(response.ContinuationValidation.HasOverlap);
        Assert.Equal(5, response.Page2.StartingMoveNumber);
        Assert.Contains(response.ContinuationValidation.Warnings, w => w.Contains("normalized", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("5. O-O Be7", response.UpdatedPgn);
        _gameRepositoryMock.Verify(x => x.UpdateAsync(It.Is<ChessGame>(g => g.Id == gameId && g.HasContinuation)), Times.Once);
    }

    private static ChessDecoderApi.Models.ValidatedMove CreateMove(string notation)
    {
        return new ChessDecoderApi.Models.ValidatedMove
        {
            Notation = notation,
            NormalizedNotation = notation,
            ValidationStatus = "valid",
            ValidationText = string.Empty
        };
    }

    private static IFormFile CreateImageFile(string fileName)
    {
        var fileMock = new Mock<IFormFile>();
        var content = new byte[] { 1, 2, 3, 4 };

        fileMock.SetupGet(x => x.FileName).Returns(fileName);
        fileMock.SetupGet(x => x.ContentType).Returns("image/jpeg");
        fileMock.SetupGet(x => x.Length).Returns(content.Length);
        fileMock.Setup(x => x.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken _) => stream.WriteAsync(content, 0, content.Length));

        return fileMock.Object;
    }
}
