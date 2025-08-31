using ChessDecoderApi.Data;
using ChessDecoderApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessDecoderApi.Services;

public class CreditService : ICreditService
{
    private readonly ChessDecoderDbContext _context;
    private readonly ILogger<CreditService> _logger;

    public CreditService(ChessDecoderDbContext context, ILogger<CreditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> HasEnoughCreditsAsync(string userId, int requiredCredits = 1)
    {
        try
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.Credits })
                .FirstOrDefaultAsync();

            return user?.Credits >= requiredCredits;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking credits for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> DeductCreditsAsync(string userId, int creditsToDeduct = 1)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for credit deduction", userId);
                return false;
            }

            if (user.Credits < creditsToDeduct)
            {
                _logger.LogWarning("User {UserId} has insufficient credits. Current: {Current}, Required: {Required}", 
                    userId, user.Credits, creditsToDeduct);
                return false;
            }

            user.Credits -= creditsToDeduct;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deducted {Credits} credits from user {UserId}. New balance: {NewBalance}", 
                creditsToDeduct, userId, user.Credits);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deducting credits for user {UserId}", userId);
            return false;
        }
    }

    public async Task<int> GetUserCreditsAsync(string userId)
    {
        try
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Credits)
                .FirstOrDefaultAsync();

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting credits for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<bool> AddCreditsAsync(string userId, int creditsToAdd)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for adding credits", userId);
                return false;
            }

            user.Credits += creditsToAdd;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added {Credits} credits to user {UserId}. New balance: {NewBalance}", 
                creditsToAdd, userId, user.Credits);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding credits for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> RefundCreditsAsync(string userId, int creditsToRefund)
    {
        return await AddCreditsAsync(userId, creditsToRefund);
    }
}
