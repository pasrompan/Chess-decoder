using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Services.GameProcessing;
using Microsoft.AspNetCore.Mvc;

namespace ChessDecoderApi.Controllers;

/// <summary>
/// Controller for project and history operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        IProjectService projectService,
        ILogger<ProjectController> logger)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get project information by game ID
    /// </summary>
    [HttpGet("{gameId}")]
    [ProducesResponseType(typeof(ProjectInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProject(Guid gameId)
    {
        try
        {
            var history = await _projectService.GetProjectByGameIdAsync(gameId);
            
            if (history == null)
            {
                return NotFound(new { message = "Project not found" });
            }

            return Ok(MapToProjectInfo(history));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to retrieve project" });
        }
    }

    /// <summary>
    /// Get full project history by game ID
    /// </summary>
    [HttpGet("{gameId}/history")]
    [ProducesResponseType(typeof(ProjectHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProjectHistory(Guid gameId)
    {
        try
        {
            var history = await _projectService.GetProjectByGameIdAsync(gameId);
            
            if (history == null)
            {
                return NotFound(new { message = "Project history not found" });
            }

            return Ok(MapToProjectHistory(history));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project history for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to retrieve project history" });
        }
    }

    /// <summary>
    /// Get all projects for a user
    /// </summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(UserProjectsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserProjects(string userId)
    {
        try
        {
            var projects = await _projectService.GetUserProjectsAsync(userId);
            
            return Ok(new UserProjectsResponse
            {
                Projects = projects.Select(MapToProjectInfo).ToList(),
                TotalCount = projects.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for user {UserId}", userId);
            return StatusCode(500, new { message = "Failed to retrieve user projects" });
        }
    }

    /// <summary>
    /// Add a history entry to a project
    /// </summary>
    [HttpPost("{gameId}/history")]
    [ProducesResponseType(typeof(ProjectHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddHistoryEntry(Guid gameId, [FromBody] AddHistoryEntryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ChangeType))
            {
                return BadRequest(new { message = "ChangeType is required" });
            }

            var history = await _projectService.AddHistoryEntryAsync(
                gameId, 
                request.ChangeType, 
                request.Description ?? "", 
                request.Changes);
            
            if (history == null)
            {
                return NotFound(new { message = "Project not found" });
            }

            return Ok(MapToProjectHistory(history));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding history entry for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to add history entry" });
        }
    }

    private static ProjectInfoResponse MapToProjectInfo(Models.ProjectHistory history)
    {
        return new ProjectInfoResponse
        {
            ProjectId = history.Id,
            GameId = history.GameId,
            UserId = history.UserId,
            CreatedAt = history.CreatedAt,
            InitialUpload = history.InitialUpload != null ? new InitialUploadDto
            {
                FileName = history.InitialUpload.FileName,
                FileSize = history.InitialUpload.FileSize,
                FileType = history.InitialUpload.FileType,
                UploadedAt = history.InitialUpload.UploadedAt,
                StorageLocation = history.InitialUpload.StorageLocation,
                StorageUrl = history.InitialUpload.StorageUrl
            } : null,
            Processing = history.Processing != null ? new ProcessingDto
            {
                ProcessedAt = history.Processing.ProcessedAt,
                PgnContent = history.Processing.PgnContent,
                ValidationStatus = history.Processing.ValidationStatus,
                ProcessingTimeMs = history.Processing.ProcessingTimeMs
            } : null,
            VersionCount = history.Versions.Count
        };
    }

    private static ProjectHistoryResponse MapToProjectHistory(Models.ProjectHistory history)
    {
        return new ProjectHistoryResponse
        {
            ProjectId = history.Id,
            GameId = history.GameId,
            UserId = history.UserId,
            CreatedAt = history.CreatedAt,
            InitialUpload = history.InitialUpload != null ? new InitialUploadDto
            {
                FileName = history.InitialUpload.FileName,
                FileSize = history.InitialUpload.FileSize,
                FileType = history.InitialUpload.FileType,
                UploadedAt = history.InitialUpload.UploadedAt,
                StorageLocation = history.InitialUpload.StorageLocation,
                StorageUrl = history.InitialUpload.StorageUrl
            } : null,
            Processing = history.Processing != null ? new ProcessingDto
            {
                ProcessedAt = history.Processing.ProcessedAt,
                PgnContent = history.Processing.PgnContent,
                ValidationStatus = history.Processing.ValidationStatus,
                ProcessingTimeMs = history.Processing.ProcessingTimeMs
            } : null,
            Versions = history.Versions.Select(v => new HistoryEntryDto
            {
                Version = v.Version,
                Timestamp = v.Timestamp,
                ChangeType = v.ChangeType,
                Description = v.Description,
                Changes = v.Changes
            }).ToList()
        };
    }
}

/// <summary>
/// Request model for adding a history entry
/// </summary>
public class AddHistoryEntryRequest
{
    public string ChangeType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, object>? Changes { get; set; }
}
