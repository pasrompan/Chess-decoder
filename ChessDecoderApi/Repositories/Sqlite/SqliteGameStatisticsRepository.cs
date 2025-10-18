using Microsoft.EntityFrameworkCore;
using ChessDecoderApi.Data;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Sqlite;

/// <summary>
/// SQLite/Entity Framework implementation of IGameStatisticsRepository
/// </summary>
public class SqliteGameStatisticsRepository : IGameStatisticsRepository
{
    private readonly ChessDecoderDbContext _context;
    private readonly ILogger<SqliteGameStatisticsRepository> _logger;

    public SqliteGameStatisticsRepository(ChessDecoderDbContext context, ILogger<SqliteGameStatisticsRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameStatistics?> GetByIdAsync(Guid id)
    {
        return await _context.GameStatistics.FindAsync(id);
    }

    public async Task<GameStatistics?> GetByChessGameIdAsync(Guid chessGameId)
    {
        return await _context.GameStatistics
            .FirstOrDefaultAsync(s => s.ChessGameId == chessGameId);
    }

    public async Task<GameStatistics> CreateAsync(GameStatistics statistics)
    {
        // Generate new Guid if not set
        if (statistics.Id == Guid.Empty)
        {
            statistics.Id = Guid.NewGuid();
        }
        
        statistics.CreatedAt = DateTime.UtcNow;
        
        _context.GameStatistics.Add(statistics);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Created statistics: {StatsId}", statistics.Id);
        return statistics;
    }

    public async Task<GameStatistics> UpdateAsync(GameStatistics statistics)
    {
        _context.GameStatistics.Update(statistics);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Updated statistics: {StatsId}", statistics.Id);
        return statistics;
    }

    public async Task<GameStatistics> CreateOrUpdateAsync(GameStatistics statistics)
    {
        // Check if statistics already exist for this game
        var existing = await GetByChessGameIdAsync(statistics.ChessGameId);
        
        if (existing != null)
        {
            statistics.Id = existing.Id;
            return await UpdateAsync(statistics);
        }
        else
        {
            return await CreateAsync(statistics);
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var stats = await GetByIdAsync(id);
            if (stats == null) return false;
            
            _context.GameStatistics.Remove(stats);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("[SQLite] Deleted statistics: {StatsId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQLite] Error deleting statistics: {StatsId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteByChessGameIdAsync(Guid chessGameId)
    {
        try
        {
            var stats = await GetByChessGameIdAsync(chessGameId);
            if (stats != null)
            {
                _context.GameStatistics.Remove(stats);
                await _context.SaveChangesAsync();
            }
            
            _logger.LogInformation("[SQLite] Deleted statistics for game: {GameId}", chessGameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQLite] Error deleting statistics for game: {GameId}", chessGameId);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.GameStatistics.AnyAsync(s => s.Id == id);
    }
}

