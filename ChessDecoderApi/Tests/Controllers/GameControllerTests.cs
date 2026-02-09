using ChessDecoderApi.Controllers;
using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Services.GameProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Controllers;

public class GameControllerTests
{
    private readonly Mock<IGameProcessingService> _gameProcessingServiceMock;
    private readonly Mock<IGameManagementService> _gameManagementServiceMock;
    private readonly Mock<ILogger<GameController>> _loggerMock;
    private readonly GameController _controller;

    public GameControllerTests()
    {
        _gameProcessingServiceMock = new Mock<IGameProcessingService>();
        _gameManagementServiceMock = new Mock<IGameManagementService>();
        _loggerMock = new Mock<ILogger<GameController>>();
        _controller = new GameController(
            _gameProcessingServiceMock.Object,
            _gameManagementServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void CheckHealth_ReturnsOkResult()
    {
        // Act
        var result = _controller.CheckHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task UploadGame_Success_ReturnsOk()
    {
        // Arrange
        var request = CreateMockGameUploadRequest();
        var expectedResponse = new GameProcessingResponse
        {
            GameId = Guid.NewGuid(),
            PgnContent = "1. e4 e5",
            CreditsRemaining = 9
        };
        _gameProcessingServiceMock
            .Setup(x => x.ProcessGameUploadAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.UploadGame(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GameProcessingResponse>(okResult.Value);
        Assert.NotNull(response.PgnContent);
    }

    [Fact]
    public async Task UploadGame_InvalidOperation_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateMockGameUploadRequest();
        _gameProcessingServiceMock
            .Setup(x => x.ProcessGameUploadAsync(request))
            .ThrowsAsync(new InvalidOperationException("Insufficient credits"));

        // Act
        var result = await _controller.UploadGame(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task UploadGame_UnexpectedException_ReturnsInternalServerError()
    {
        // Arrange
        var request = CreateMockGameUploadRequest();
        _gameProcessingServiceMock
            .Setup(x => x.ProcessGameUploadAsync(request))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.UploadGame(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetGame_ExistingGame_ReturnsOk()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var gameDetails = new GameDetailsResponse { GameId = gameId };
        _gameManagementServiceMock.Setup(x => x.GetGameByIdAsync(gameId)).ReturnsAsync(gameDetails);

        // Act
        var result = await _controller.GetGame(gameId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GameDetailsResponse>(okResult.Value);
        Assert.Equal(gameId, response.GameId);
    }

    [Fact]
    public async Task GetGame_NonExistingGame_ReturnsNotFound()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameManagementServiceMock.Setup(x => x.GetGameByIdAsync(gameId)).ReturnsAsync((GameDetailsResponse?)null);

        // Act
        var result = await _controller.GetGame(gameId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateGameMetadata_Success_ReturnsOk()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new UpdateGameMetadataRequest
        {
            WhitePlayer = "John Doe",
            BlackPlayer = "Jane Smith",
            GameDate = new DateTime(2025, 12, 7),
            Round = "1"
        };
        _gameManagementServiceMock
            .Setup(x => x.UpdateGameMetadataAsync(gameId, request))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateGameMetadata(gameId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task UpdateGameMetadata_GameNotFound_ReturnsNotFound()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new UpdateGameMetadataRequest
        {
            WhitePlayer = "John Doe"
        };
        _gameManagementServiceMock
            .Setup(x => x.UpdateGameMetadataAsync(gameId, request))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateGameMetadata(gameId, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task UpdateGameMetadata_Exception_ReturnsInternalServerError()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new UpdateGameMetadataRequest
        {
            WhitePlayer = "John Doe"
        };
        _gameManagementServiceMock
            .Setup(x => x.UpdateGameMetadataAsync(gameId, request))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.UpdateGameMetadata(gameId, request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetGame_WithMetadata_ReturnsGameWithMetadata()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var gameDetails = new GameDetailsResponse
        {
            GameId = gameId,
            UserId = "test-user",
            Title = "Test Game",
            PgnContent = "[Date \"2025.12.07\"]\n[White \"John Doe\"]\n[Black \"Jane Smith\"]\n\n1. e4 e5 *",
            WhitePlayer = "John Doe",
            BlackPlayer = "Jane Smith",
            GameDate = new DateTime(2025, 12, 7),
            Round = "1"
        };
        _gameManagementServiceMock
            .Setup(x => x.GetGameByIdAsync(gameId))
            .ReturnsAsync(gameDetails);

        // Act
        var result = await _controller.GetGame(gameId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GameDetailsResponse>(okResult.Value);
        Assert.Equal("John Doe", response.WhitePlayer);
        Assert.Equal("Jane Smith", response.BlackPlayer);
        Assert.Equal(new DateTime(2025, 12, 7), response.GameDate);
        Assert.Equal("1", response.Round);
    }

    [Fact]
    public async Task UpdateGamePgn_Success_ReturnsOkWithUpdatedGame()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var request = new UpdatePgnRequest { PgnContent = "1. d4 d5 2. c4 e6" };
        var expectedResponse = new GameDetailsResponse
        {
            GameId = gameId,
            UserId = userId,
            PgnContent = request.PgnContent,
            EditCount = 1,
            LastEditedAt = DateTime.UtcNow
        };
        
        _gameManagementServiceMock
            .Setup(x => x.UpdatePgnContentAsync(gameId, userId, request.PgnContent))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.UpdateGamePgn(gameId, request, userId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GameDetailsResponse>(okResult.Value);
        Assert.Equal(request.PgnContent, response.PgnContent);
        Assert.Equal(1, response.EditCount);
    }

    [Fact]
    public async Task UpdateGamePgn_MissingUserId_ReturnsBadRequest()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new UpdatePgnRequest { PgnContent = "1. e4 e5" };

        // Act
        var result = await _controller.UpdateGamePgn(gameId, request, "");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateGamePgn_EmptyPgnContent_ReturnsBadRequest()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var request = new UpdatePgnRequest { PgnContent = "" };

        // Act
        var result = await _controller.UpdateGamePgn(gameId, request, userId);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateGamePgn_GameNotFound_ReturnsNotFound()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var request = new UpdatePgnRequest { PgnContent = "1. e4 e5" };
        
        _gameManagementServiceMock
            .Setup(x => x.UpdatePgnContentAsync(gameId, userId, request.PgnContent))
            .ReturnsAsync((GameDetailsResponse?)null);

        // Act
        var result = await _controller.UpdateGamePgn(gameId, request, userId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task MarkProcessingComplete_Success_ReturnsOkWithUpdatedGame()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var expectedResponse = new GameDetailsResponse
        {
            GameId = gameId,
            UserId = userId,
            ProcessingCompleted = true
        };
        
        _gameManagementServiceMock
            .Setup(x => x.MarkProcessingCompleteAsync(gameId, userId))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.MarkProcessingComplete(gameId, userId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GameDetailsResponse>(okResult.Value);
        Assert.True(response.ProcessingCompleted);
    }

    [Fact]
    public async Task MarkProcessingComplete_MissingUserId_ReturnsBadRequest()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var result = await _controller.MarkProcessingComplete(gameId, "");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MarkProcessingComplete_GameNotFound_ReturnsNotFound()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        
        _gameManagementServiceMock
            .Setup(x => x.MarkProcessingCompleteAsync(gameId, userId))
            .ReturnsAsync((GameDetailsResponse?)null);

        // Act
        var result = await _controller.MarkProcessingComplete(gameId, userId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    private GameUploadRequest CreateMockGameUploadRequest()
    {
        var fileMock = new Mock<IFormFile>();
        var content = "fake image content";
        var fileName = "test.jpg";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;
        
        fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
        fileMock.Setup(_ => _.FileName).Returns(fileName);
        fileMock.Setup(_ => _.Length).Returns(ms.Length);
        fileMock.Setup(_ => _.ContentType).Returns("image/jpeg");

        return new GameUploadRequest
        {
            Image = fileMock.Object,
            UserId = "test-user",
            AutoCrop = false
        };
    }
}

