using ChessDecoderApi.Models;

namespace ChessDecoderApi.Repositories.Interfaces;

/// <summary>
/// Repository interface for User data access operations
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Get a user by their unique ID
    /// </summary>
    Task<User?> GetByIdAsync(string id);

    /// <summary>
    /// Get a user by their email address
    /// </summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Create a new user
    /// </summary>
    Task<User> CreateAsync(User user);

    /// <summary>
    /// Update an existing user
    /// </summary>
    Task<User> UpdateAsync(User user);

    /// <summary>
    /// Delete a user by ID
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Check if a user exists by ID
    /// </summary>
    Task<bool> ExistsAsync(string id);

    /// <summary>
    /// Get user's credit balance
    /// </summary>
    Task<int> GetCreditsAsync(string userId);

    /// <summary>
    /// Update user's credit balance
    /// </summary>
    Task<bool> UpdateCreditsAsync(string userId, int credits);
}

