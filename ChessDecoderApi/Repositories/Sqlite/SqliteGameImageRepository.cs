using Microsoft.EntityFrameworkCore;
using ChessDecoderApi.Data;
using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Repositories.Sqlite;

/// <summary>
/// SQLite/Entity Framework implementation of IGameImageRepository
/// </summary>
public class SqliteGameImageRepository : IGameImageRepository
{
    private readonly ChessDecoderDbContext _context;
    private readonly ILogger<SqliteGameImageRepository> _logger;

    public SqliteGameImageRepository(ChessDecoderDbContext context, ILogger<SqliteGameImageRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameImage?> GetByIdAsync(Guid id)
    {
        return await _context.GameImages.FindAsync(id);
    }

    public async Task<List<GameImage>> GetByChessGameIdAsync(Guid chessGameId)
    {
        return await _context.GameImages
            .Where(i => i.ChessGameId == chessGameId)
            .ToListAsync();
    }

    public async Task<GameImage> CreateAsync(GameImage image)
    {
        // Generate new Guid if not set
        if (image.Id == Guid.Empty)
        {
            image.Id = Guid.NewGuid();
        }
        
        image.UploadedAt = DateTime.UtcNow;
        
        _context.GameImages.Add(image);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Created image: {ImageId}", image.Id);
        return image;
    }

    public async Task<GameImage> UpdateAsync(GameImage image)
    {
        _context.GameImages.Update(image);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("[SQLite] Updated image: {ImageId}", image.Id);
        return image;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var image = await GetByIdAsync(id);
            if (image == null) return false;
            
            _context.GameImages.Remove(image);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("[SQLite] Deleted image: {ImageId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQLite] Error deleting image: {ImageId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteByChessGameIdAsync(Guid chessGameId)
    {
        try
        {
            var images = await GetByChessGameIdAsync(chessGameId);
            _context.GameImages.RemoveRange(images);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("[SQLite] Deleted all images for game: {GameId}", chessGameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQLite] Error deleting images for game: {GameId}", chessGameId);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.GameImages.AnyAsync(i => i.Id == id);
    }
}

