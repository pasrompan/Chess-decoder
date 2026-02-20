using Microsoft.EntityFrameworkCore;
using ChessDecoderApi.Data;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Sqlite;

/// <summary>
/// SQLite/Entity Framework implementation of IChessGameRepository
/// </summary>
public class SqliteChessGameRepository : IChessGameRepository
{
    private readonly ChessDecoderDbContext _context;
    private readonly ILogger<SqliteChessGameRepository> _logger;

    public SqliteChessGameRepository(ChessDecoderDbContext context, ILogger<SqliteChessGameRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChessGame?> GetByIdAsync(Guid id)
    {
        return await _context.ChessGames
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);
    }

    public async Task<List<ChessGame>> GetByUserIdAsync(string userId)
    {
        return await _context.ChessGames
            .Where(g => g.UserId == userId && !g.IsDeleted)
            .OrderByDescending(g => g.ProcessedAt)
            .ToListAsync();
    }

    public async Task<(List<ChessGame> games, int totalCount)> GetByUserIdPaginatedAsync(
        string userId, 
        int pageNumber = 1, 
        int pageSize = 10)
    {
        var query = _context.ChessGames.Where(g => g.UserId == userId && !g.IsDeleted);
        
        var totalCount = await query.CountAsync();
        
        var games = await query
            .OrderByDescending(g => g.ProcessedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (games, totalCount);
    }

    public async Task<ChessGame> CreateAsync(ChessGame game)
    {
        // Generate new Guid if not set
        if (game.Id == Guid.Empty)
        {
            game.Id = Guid.NewGuid();
        }
        
        game.ProcessedAt = DateTime.UtcNow;
        game.IsDeleted = false;
        game.DeletedAt = null;
        
        _context.ChessGames.Add(game);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Created game: {GameId}", game.Id);
        return game;
    }

    public async Task<ChessGame> UpdateAsync(ChessGame game)
    {
        _context.ChessGames.Update(game);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Updated game: {GameId}", game.Id);
        return game;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var game = await _context.ChessGames.FirstOrDefaultAsync(g => g.Id == id);
            if (game == null) return false;
            if (game.IsDeleted) return false;

            game.IsDeleted = true;
            game.DeletedAt = DateTime.UtcNow;
            _context.ChessGames.Update(game);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("[SQLite] Soft-deleted game: {GameId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQLite] Error soft-deleting game: {GameId}", id);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.ChessGames.AnyAsync(g => g.Id == id && !g.IsDeleted);
    }

    public async Task<int> GetCountByUserIdAsync(string userId)
    {
        return await _context.ChessGames.CountAsync(g => g.UserId == userId && !g.IsDeleted);
    }
}
