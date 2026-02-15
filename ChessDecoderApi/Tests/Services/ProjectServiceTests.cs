using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Interfaces;
using ChessDecoderApi.Services;
using ChessDecoderApi.Services.GameProcessing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services;

public class ProjectServiceTests
{
    private readonly Mock<RepositoryFactory> _repositoryFactoryMock;
    private readonly Mock<IProjectHistoryRepository> _projectHistoryRepositoryMock;
    private readonly Mock<IChessGameRepository> _gameRepositoryMock;
    private readonly Mock<IGameImageRepository> _imageRepositoryMock;
    private readonly Mock<ILogger<ProjectService>> _loggerMock;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());
        _projectHistoryRepositoryMock = new Mock<IProjectHistoryRepository>();
        _gameRepositoryMock = new Mock<IChessGameRepository>();
        _imageRepositoryMock = new Mock<IGameImageRepository>();
        _loggerMock = new Mock<ILogger<ProjectService>>();

        _repositoryFactoryMock
            .Setup(x => x.CreateProjectHistoryRepositoryAsync())
            .ReturnsAsync(_projectHistoryRepositoryMock.Object);
        _repositoryFactoryMock
            .Setup(x => x.CreateChessGameRepositoryAsync())
            .ReturnsAsync(_gameRepositoryMock.Object);
        _repositoryFactoryMock
            .Setup(x => x.CreateGameImageRepositoryAsync())
            .ReturnsAsync(_imageRepositoryMock.Object);

        _service = new ProjectService(_repositoryFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateProjectAsync_Success_CreatesProjectWithInitialVersion()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var uploadData = new InitialUploadData
        {
            FileName = "test.jpg",
            FileSize = 12345,
            FileType = "image/jpeg",
            UploadedAt = DateTime.UtcNow,
            StorageLocation = "cloud",
            StorageUrl = "https://storage.example.com/test.jpg"
        };
        var processingData = new ProcessingData
        {
            ProcessedAt = DateTime.UtcNow,
            PgnContent = "1. e4 e5 *",
            ValidationStatus = "valid",
            ProcessingTimeMs = 1000
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<ProjectHistory>()))
            .ReturnsAsync((ProjectHistory h) => h);

        // Act
        var result = await _service.CreateProjectAsync(gameId, userId, uploadData, processingData);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(uploadData.FileName, result.InitialUpload?.FileName);
        Assert.Equal(processingData.PgnContent, result.Processing?.PgnContent);
        Assert.Single(result.Versions);
        Assert.Equal("initial_upload", result.Versions[0].ChangeType);
        Assert.Equal(1, result.Versions[0].Version);
        
        _projectHistoryRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<ProjectHistory>()), Times.Once);
    }

    [Fact]
    public async Task CreateProjectAsync_FirestoreNotAvailable_ReturnsEmptyHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = "test-user";
        var uploadData = new InitialUploadData { FileName = "test.jpg" };
        var processingData = new ProcessingData { PgnContent = "1. e4 *" };

        _repositoryFactoryMock
            .Setup(x => x.CreateProjectHistoryRepositoryAsync())
            .ThrowsAsync(new NotSupportedException("Firestore not available"));

        // Act
        var result = await _service.CreateProjectAsync(gameId, userId, uploadData, processingData);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task GetProjectByGameIdAsync_ExistingProject_ReturnsProject()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var expectedHistory = new ProjectHistory
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = "test-user",
            CreatedAt = DateTime.UtcNow
        };
        var game = new ChessGame
        {
            Id = gameId,
            UserId = "test-user",
            Title = "Test Game",
            PgnContent = "1. e4 e5 *"
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync(expectedHistory);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId))
            .ReturnsAsync(game);

        // Act
        var result = await _service.GetProjectByGameIdAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.GameId);
    }

    [Fact]
    public async Task GetProjectByGameIdAsync_NonExistingProject_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync((ProjectHistory?)null);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId))
            .ReturnsAsync((ChessGame?)null);

        // Act
        var result = await _service.GetProjectByGameIdAsync(gameId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProjectsAsync_ReturnsUserProjects()
    {
        // Arrange
        var userId = "test-user";
        var projects = new List<ProjectHistory>
        {
            new ProjectHistory { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), UserId = userId },
            new ProjectHistory { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), UserId = userId }
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(projects);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid gameId) => new ChessGame
            {
                Id = gameId,
                UserId = userId,
                Title = "Visible game",
                PgnContent = "1. e4 e5 *"
            });

        // Act
        var result = await _service.GetUserProjectsAsync(userId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal(userId, p.UserId));
    }

    [Fact]
    public async Task GetUserProjectsAsync_ExcludesProjectsForDeletedOrMissingGames()
    {
        // Arrange
        var userId = "test-user";
        var visibleGameId = Guid.NewGuid();
        var hiddenGameId = Guid.NewGuid();
        var projects = new List<ProjectHistory>
        {
            new ProjectHistory { Id = Guid.NewGuid(), GameId = visibleGameId, UserId = userId },
            new ProjectHistory { Id = Guid.NewGuid(), GameId = hiddenGameId, UserId = userId }
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(projects);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(visibleGameId))
            .ReturnsAsync(new ChessGame
            {
                Id = visibleGameId,
                UserId = userId,
                Title = "Visible",
                PgnContent = "1. e4 e5 *"
            });
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(hiddenGameId))
            .ReturnsAsync((ChessGame?)null);

        // Act
        var result = await _service.GetUserProjectsAsync(userId);

        // Assert
        Assert.Single(result);
        Assert.Equal(visibleGameId, result[0].GameId);
    }

    [Fact]
    public async Task AddHistoryEntryAsync_ExistingProject_AddsNewVersion()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingHistory = new ProjectHistory
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = "test-user",
            Versions = new List<HistoryEntry>
            {
                new HistoryEntry { Version = 1, ChangeType = "initial_upload", Timestamp = DateTime.UtcNow }
            }
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync(existingHistory);
        _projectHistoryRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<ProjectHistory>()))
            .ReturnsAsync((ProjectHistory h) => h);

        // Act
        var result = await _service.AddHistoryEntryAsync(gameId, "modification", "Updated PGN");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Versions.Count);
        Assert.Equal(2, result.Versions[1].Version);
        Assert.Equal("modification", result.Versions[1].ChangeType);
        Assert.Equal("Updated PGN", result.Versions[1].Description);
        
        _projectHistoryRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ProjectHistory>()), Times.Once);
    }

    [Fact]
    public async Task AddHistoryEntryAsync_NonExistingProject_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync((ProjectHistory?)null);

        // Act
        var result = await _service.AddHistoryEntryAsync(gameId, "modification", "Updated PGN");

        // Assert
        Assert.Null(result);
        _projectHistoryRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ProjectHistory>()), Times.Never);
    }

    [Fact]
    public async Task AddHistoryEntryAsync_ExistingProjectWithNullVersions_InitializesAndAddsVersion()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingHistory = new ProjectHistory
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = "test-user",
            Versions = null!
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync(existingHistory);
        _projectHistoryRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<ProjectHistory>()))
            .ReturnsAsync((ProjectHistory h) => h);

        // Act
        var result = await _service.AddHistoryEntryAsync(gameId, "modification", "Initialized versions");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Versions);
        Assert.Single(result.Versions);
        Assert.Equal(1, result.Versions[0].Version);
        Assert.Equal("modification", result.Versions[0].ChangeType);
    }

    [Fact]
    public async Task UpdateProcessingDataAsync_ExistingProject_UpdatesData()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingHistory = new ProjectHistory
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = "test-user",
            Processing = new ProcessingData { PgnContent = "old content" }
        };
        var newProcessingData = new ProcessingData
        {
            ProcessedAt = DateTime.UtcNow,
            PgnContent = "1. d4 d5 *",
            ValidationStatus = "valid",
            ProcessingTimeMs = 500
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync(existingHistory);
        _projectHistoryRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<ProjectHistory>()))
            .ReturnsAsync((ProjectHistory h) => h);

        // Act
        var result = await _service.UpdateProcessingDataAsync(gameId, newProcessingData);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newProcessingData.PgnContent, result.Processing?.PgnContent);
        
        _projectHistoryRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ProjectHistory>()), Times.Once);
    }

    [Fact]
    public async Task GetProjectByGameIdAsync_NoHistory_CreatesProjectFromExistingGameAndImage()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var processedAt = DateTime.UtcNow.AddMinutes(-5);
        var uploadedAt = DateTime.UtcNow.AddMinutes(-6);
        var game = new ChessGame
        {
            Id = gameId,
            UserId = "test-user",
            Title = "Existing game",
            ProcessedAt = processedAt,
            PgnContent = "1. e4 e5 *",
            IsValid = true,
            ProcessingTimeMs = 321
        };
        var image = new GameImage
        {
            Id = Guid.NewGuid(),
            ChessGameId = gameId,
            FileName = "board.png",
            FileSizeBytes = 2048,
            FileType = "image/png",
            UploadedAt = uploadedAt,
            IsStoredInCloud = true,
            CloudStorageUrl = "https://storage.example.com/board.png",
            Variant = "original"
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync((ProjectHistory?)null);
        _projectHistoryRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<ProjectHistory>()))
            .ReturnsAsync((ProjectHistory history) => history);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId))
            .ReturnsAsync(game);
        _imageRepositoryMock
            .Setup(x => x.GetByChessGameIdAsync(gameId))
            .ReturnsAsync(new List<GameImage> { image });

        // Act
        var result = await _service.GetProjectByGameIdAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("board.png", result.InitialUpload?.FileName);
        Assert.Equal("cloud", result.InitialUpload?.StorageLocation);
        Assert.Equal("valid", result.Processing?.ValidationStatus);
        Assert.Single(result.Versions);

        _projectHistoryRepositoryMock.Verify(x => x.CreateAsync(It.Is<ProjectHistory>(h =>
            h.GameId == gameId &&
            h.UserId == "test-user" &&
            h.InitialUpload != null &&
            h.InitialUpload.FileName == "board.png" &&
            h.InitialUpload.FileType == "image/png" &&
            h.InitialUpload.FileSize == 2048 &&
            h.InitialUpload.StorageLocation == "cloud" &&
            h.Processing != null &&
            h.Processing.PgnContent == "1. e4 e5 *" &&
            h.Processing.ValidationStatus == "valid" &&
            h.Processing.ProcessingTimeMs == 321
        )), Times.Once);
    }

    [Fact]
    public async Task GetProjectByGameIdAsync_NoHistoryOrImages_UsesFallbackUploadMetadata()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var processedAt = DateTime.UtcNow.AddMinutes(-2);
        var game = new ChessGame
        {
            Id = gameId,
            UserId = "test-user",
            Title = "Existing game",
            ProcessedAt = processedAt,
            PgnContent = "1. d4 d5 *",
            IsValid = false,
            ProcessingTimeMs = 450
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync((ProjectHistory?)null);
        _projectHistoryRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<ProjectHistory>()))
            .ReturnsAsync((ProjectHistory history) => history);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId))
            .ReturnsAsync(game);
        _imageRepositoryMock
            .Setup(x => x.GetByChessGameIdAsync(gameId))
            .ReturnsAsync(new List<GameImage>());

        // Act
        var result = await _service.GetProjectByGameIdAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unknown", result.InitialUpload?.FileName);
        Assert.Equal(0, result.InitialUpload?.FileSize);
        Assert.Equal("local", result.InitialUpload?.StorageLocation);
        Assert.Equal("invalid", result.Processing?.ValidationStatus);
        Assert.Equal(processedAt, result.InitialUpload?.UploadedAt);
    }

    [Fact]
    public async Task GetUserProjectsAsync_FirestoreNotSupported_ReturnsEmptyList()
    {
        // Arrange
        var userId = "test-user";
        _repositoryFactoryMock
            .Setup(x => x.CreateProjectHistoryRepositoryAsync())
            .ThrowsAsync(new NotSupportedException("Firestore not configured"));

        // Act
        var result = await _service.GetUserProjectsAsync(userId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateProcessingDataAsync_NonExistingProject_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var processingData = new ProcessingData
        {
            ProcessedAt = DateTime.UtcNow,
            PgnContent = "1. c4 e5 *",
            ValidationStatus = "valid",
            ProcessingTimeMs = 200
        };

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync((ProjectHistory?)null);

        // Act
        var result = await _service.UpdateProcessingDataAsync(gameId, processingData);

        // Assert
        Assert.Null(result);
        _projectHistoryRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ProjectHistory>()), Times.Never);
    }

    [Fact]
    public async Task EnsureProjectForMockResponseAsync_WhenMissing_CreatesProjectWithMockUser()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        const string pgn = "1. e4 e5 2. Nf3 Nc6 *";

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync((ProjectHistory?)null);
        _projectHistoryRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<ProjectHistory>()))
            .ReturnsAsync((ProjectHistory history) => history);

        // Act
        await _service.EnsureProjectForMockResponseAsync(gameId, pgn);

        // Assert
        _projectHistoryRepositoryMock.Verify(x => x.CreateAsync(It.Is<ProjectHistory>(h =>
            h.GameId == gameId &&
            h.UserId == "mock-user" &&
            h.InitialUpload != null &&
            h.InitialUpload.FileName == "mock-upload.jpg" &&
            h.Processing != null &&
            h.Processing.PgnContent == pgn &&
            h.Versions.Count == 1 &&
            h.Versions[0].ChangeType == "initial_upload"
        )), Times.Once);
    }

    [Fact]
    public async Task EnsureProjectForMockResponseAsync_WhenExisting_DoesNotCreateNewProject()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync(new ProjectHistory
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                UserId = "mock-user"
            });

        // Act
        await _service.EnsureProjectForMockResponseAsync(gameId, "1. d4 d5 *");

        // Assert
        _projectHistoryRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<ProjectHistory>()), Times.Never);
    }
}
