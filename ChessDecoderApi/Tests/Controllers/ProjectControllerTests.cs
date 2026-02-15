using ChessDecoderApi.Controllers;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Models;
using ChessDecoderApi.Services.GameProcessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChessDecoderApi.Tests.Controllers;

public class ProjectControllerTests
{
    private readonly Mock<IProjectService> _projectServiceMock;
    private readonly Mock<ILogger<ProjectController>> _loggerMock;
    private readonly ProjectController _controller;

    public ProjectControllerTests()
    {
        _projectServiceMock = new Mock<IProjectService>();
        _loggerMock = new Mock<ILogger<ProjectController>>();
        _controller = new ProjectController(_projectServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetProject_ExistingProject_ReturnsMappedProjectInfo()
    {
        // Arrange
        var history = CreateProjectHistory();
        _projectServiceMock
            .Setup(x => x.GetProjectByGameIdAsync(history.GameId))
            .ReturnsAsync(history);

        // Act
        var result = await _controller.GetProject(history.GameId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProjectInfoResponse>(okResult.Value);
        Assert.Equal(history.Id, response.ProjectId);
        Assert.Equal(history.GameId, response.GameId);
        Assert.Equal(history.UserId, response.UserId);
        Assert.Equal(history.Versions.Count, response.VersionCount);
        Assert.Equal(history.InitialUpload?.FileName, response.InitialUpload?.FileName);
    }

    [Fact]
    public async Task GetProject_MissingProject_ReturnsNotFound()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _projectServiceMock
            .Setup(x => x.GetProjectByGameIdAsync(gameId))
            .ReturnsAsync((ProjectHistory?)null);

        // Act
        var result = await _controller.GetProject(gameId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetProjectHistory_ExistingProject_ReturnsMappedHistory()
    {
        // Arrange
        var history = CreateProjectHistory();
        _projectServiceMock
            .Setup(x => x.GetProjectByGameIdAsync(history.GameId))
            .ReturnsAsync(history);

        // Act
        var result = await _controller.GetProjectHistory(history.GameId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProjectHistoryResponse>(okResult.Value);
        Assert.Equal(history.Id, response.ProjectId);
        Assert.Equal(2, response.Versions.Count);
        Assert.Equal("modification", response.Versions[1].ChangeType);
    }

    [Fact]
    public async Task AddHistoryEntry_EmptyChangeType_ReturnsBadRequest()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var result = await _controller.AddHistoryEntry(gameId, new AddHistoryEntryRequest
        {
            ChangeType = " "
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddHistoryEntry_ProjectMissing_ReturnsNotFound()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _projectServiceMock
            .Setup(x => x.AddHistoryEntryAsync(gameId, "update", "Updated", null))
            .ReturnsAsync((ProjectHistory?)null);

        // Act
        var result = await _controller.AddHistoryEntry(gameId, new AddHistoryEntryRequest
        {
            ChangeType = "update",
            Description = "Updated"
        });

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetUserProjects_ReturnsProjectsAndCount()
    {
        // Arrange
        const string userId = "test-user";
        var projects = new List<ProjectHistory>
        {
            CreateProjectHistory(),
            CreateProjectHistory()
        };
        _projectServiceMock
            .Setup(x => x.GetUserProjectsAsync(userId))
            .ReturnsAsync(projects);

        // Act
        var result = await _controller.GetUserProjects(userId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserProjectsResponse>(okResult.Value);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Projects.Count);
    }

    private static ProjectHistory CreateProjectHistory()
    {
        var gameId = Guid.NewGuid();
        return new ProjectHistory
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = "test-user",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            InitialUpload = new InitialUploadData
            {
                FileName = "game.jpg",
                FileSize = 1024,
                FileType = "image/jpeg",
                UploadedAt = DateTime.UtcNow.AddMinutes(-11),
                StorageLocation = "local"
            },
            Processing = new ProcessingData
            {
                ProcessedAt = DateTime.UtcNow.AddMinutes(-10),
                PgnContent = "1. e4 e5 *",
                ValidationStatus = "valid",
                ProcessingTimeMs = 500
            },
            Versions = new List<HistoryEntry>
            {
                new()
                {
                    Version = 1,
                    Timestamp = DateTime.UtcNow.AddMinutes(-10),
                    ChangeType = "initial_upload",
                    Description = "Initial image upload and processing"
                },
                new()
                {
                    Version = 2,
                    Timestamp = DateTime.UtcNow.AddMinutes(-5),
                    ChangeType = "modification",
                    Description = "Updated PGN"
                }
            }
        };
    }
}
