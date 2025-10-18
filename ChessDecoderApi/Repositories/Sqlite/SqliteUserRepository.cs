using Microsoft.EntityFrameworkCore;
using ChessDecoderApi.Data;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Sqlite;

/// <summary>
/// SQLite/Entity Framework implementation of IUserRepository
/// </summary>
public class SqliteUserRepository : IUserRepository
{
    private readonly ChessDecoderDbContext _context;
    private readonly ILogger<SqliteUserRepository> _logger;

    public SqliteUserRepository(ChessDecoderDbContext context, ILogger<SqliteUserRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.LastLoginAt = DateTime.UtcNow;
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Created user: {UserId}", user.Id);
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Updated user: {UserId}", user.Id);
        return user;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            var user = await GetByIdAsync(id);
            if (user == null) return false;
            
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("[SQLite] Deleted user: {UserId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQLite] Error deleting user: {UserId}", id);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id);
    }

    public async Task<int> GetCreditsAsync(string userId)
    {
        var user = await GetByIdAsync(userId);
        return user?.Credits ?? 0;
    }

    public async Task<bool> UpdateCreditsAsync(string userId, int credits)
    {
        try
        {
            var user = await GetByIdAsync(userId);
            if (user == null) return false;
            
            user.Credits = credits;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("[SQLite] Updated credits for user {UserId} to {Credits}", userId, credits);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQLite] Error updating credits for user: {UserId}", userId);
            return false;
        }
    }
}

