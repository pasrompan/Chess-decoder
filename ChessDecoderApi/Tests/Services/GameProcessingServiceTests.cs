using ChessDecoderApi.DTOs.Requests;
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

public class GameProcessingServiceTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ICreditService> _creditServiceMock;
    private readonly Mock<ICloudStorageService> _cloudStorageServiceMock;
    private readonly Mock<IImageExtractionService> _imageExtractionServiceMock;
    private readonly Mock<IImageManipulationService> _imageManipulationServiceMock;
    private readonly Mock<IImageProcessingService> _legacyImageProcessingServiceMock;
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IChessGameRepository> _gameRepositoryMock;
    private readonly Mock<ILogger<GameProcessingService>> _loggerMock;
    private readonly GameProcessingService _service;

    public GameProcessingServiceTests()
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
        _loggerMock = new Mock<ILogger<GameProcessingService>>();

        _repositoryFactoryMock
            .Setup(x => x.CreateChessGameRepositoryAsync())
            .ReturnsAsync(_gameRepositoryMock.Object);

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
    public async Task ProcessGameUploadAsync_UserNotFound_ThrowsException()
    {
        // Arrange
        var request = CreateMockGameUploadRequest();
        _authServiceMock.Setup(x => x.GetUserProfileAsync(request.UserId)).ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.ProcessGameUploadAsync(request));
    }

    [Fact]
    public async Task ProcessGameUploadAsync_InsufficientCredits_ThrowsException()
    {
        // Arrange
        var request = CreateMockGameUploadRequest();
        var user = TestDataBuilder.CreateUser(id: request.UserId, credits: 0);
        _authServiceMock.Setup(x => x.GetUserProfileAsync(request.UserId)).ReturnsAsync(user);
        _creditServiceMock.Setup(x => x.HasEnoughCreditsAsync(request.UserId, 1)).ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.ProcessGameUploadAsync(request));
        Assert.Contains("Insufficient credits", exception.Message);
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
        fileMock.Setup(_ => _.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream, token));

        return new GameUploadRequest
        {
            Image = fileMock.Object,
            UserId = "test-user",
            Language = "English",
            AutoCrop = false,
            ExpectedColumns = 4
        };
    }
}

