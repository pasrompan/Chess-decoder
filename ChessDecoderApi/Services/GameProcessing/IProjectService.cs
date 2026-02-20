using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for managing project history and tracking
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Create a new project history for a game
    /// </summary>
    Task<ProjectHistory> CreateProjectAsync(Guid gameId, string userId, InitialUploadData uploadData, ProcessingData processingData);
    
    /// <summary>
    /// Get project history by game ID
    /// </summary>
    Task<ProjectHistory?> GetProjectByGameIdAsync(Guid gameId);
    
    /// <summary>
    /// Get all projects for a user
    /// </summary>
    Task<List<ProjectHistory>> GetUserProjectsAsync(string userId);
    
    /// <summary>
    /// Add a history entry to a project
    /// </summary>
    Task<ProjectHistory?> AddHistoryEntryAsync(Guid gameId, string changeType, string description, Dictionary<string, object>? changes = null);
    
    /// <summary>
    /// Update processing data for a project
    /// </summary>
    Task<ProjectHistory?> UpdateProcessingDataAsync(Guid gameId, ProcessingData processingData);

    /// <summary>
    /// Ensure project history exists for a mock upload (so project page works when mock API is enabled).
    /// Idempotent: no-op if project already exists for the game ID.
    /// </summary>
    Task EnsureProjectForMockResponseAsync(Guid gameId, string pgnContent);
}
