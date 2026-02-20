using ChessDecoderApi.Models;

namespace ChessDecoderApi.Repositories.Interfaces;

/// <summary>
/// Repository interface for ProjectHistory data access operations
/// </summary>
public interface IProjectHistoryRepository
{
    /// <summary>
    /// Get project history by its unique ID
    /// </summary>
    Task<ProjectHistory?> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get project history by game ID
    /// </summary>
    Task<ProjectHistory?> GetByGameIdAsync(Guid gameId);
    
    /// <summary>
    /// Get all project histories for a specific user
    /// </summary>
    Task<List<ProjectHistory>> GetByUserIdAsync(string userId);
    
    /// <summary>
    /// Create a new project history
    /// </summary>
    Task<ProjectHistory> CreateAsync(ProjectHistory history);
    
    /// <summary>
    /// Update an existing project history
    /// </summary>
    Task<ProjectHistory> UpdateAsync(ProjectHistory history);
    
    /// <summary>
    /// Delete project history by ID
    /// </summary>
    Task<bool> DeleteAsync(Guid id);
    
    /// <summary>
    /// Delete project history by game ID
    /// </summary>
    Task<bool> DeleteByGameIdAsync(Guid gameId);
}
