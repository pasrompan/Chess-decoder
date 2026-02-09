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
    private readonly Mock<ILogger<ProjectService>> _loggerMock;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _repositoryFactoryMock = new Mock<RepositoryFactory>(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IFirestoreService>(),
            Mock.Of<ILogger<RepositoryFactory>>());
        _projectHistoryRepositoryMock = new Mock<IProjectHistoryRepository>();
        _loggerMock = new Mock<ILogger<ProjectService>>();

        _repositoryFactoryMock
            .Setup(x => x.CreateProjectHistoryRepositoryAsync())
            .ReturnsAsync(_projectHistoryRepositoryMock.Object);

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

        _projectHistoryRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId))
            .ReturnsAsync(expectedHistory);

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

        // Act
        var result = await _service.GetUserProjectsAsync(userId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal(userId, p.UserId));
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
}
