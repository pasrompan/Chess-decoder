using ChessDecoderApi.Data;
using ChessDecoderApi.Models;
using ChessDecoderApi.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ChessDecoderApi.Services;

public class CreditService : ICreditService
{
    private readonly IFirestoreService _firestore;
    private readonly ChessDecoderDbContext _context;
    private readonly ILogger<CreditService> _logger;

    public CreditService(IFirestoreService firestore, ChessDecoderDbContext context, ILogger<CreditService> logger)
    {
        _firestore = firestore;
        _context = context;
        _logger = logger;
    }

    public async Task<bool> HasEnoughCreditsAsync(string userId, int requiredCredits = 1)
    {
        try
        {
            // Try Firestore first, fall back to EF
            if (await _firestore.IsAvailableAsync())
            {
                _logger.LogDebug("[Credits] Using Firestore to check credits for user: {UserId}", userId);
                var user = await _firestore.GetUserByIdAsync(userId);
                return user?.Credits >= requiredCredits;
            }
            
            _logger.LogDebug("[Credits] Using SQLite/EF to check credits for user: {UserId}", userId);
            var efUser = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.Credits })
                .FirstOrDefaultAsync();

            return efUser?.Credits >= requiredCredits;
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
            // Try Firestore first, fall back to EF
            if (await _firestore.IsAvailableAsync())
            {
                _logger.LogDebug("[Credits] Using Firestore to deduct credits for user: {UserId}", userId);
                var user = await _firestore.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found in Firestore for credit deduction", userId);
                    return false;
                }

                if (user.Credits < creditsToDeduct)
                {
                    _logger.LogWarning("User {UserId} has insufficient credits. Current: {Current}, Required: {Required}", 
                        userId, user.Credits, creditsToDeduct);
                    return false;
                }

                user.Credits -= creditsToDeduct;
                await _firestore.UpdateUserAsync(user);

                _logger.LogInformation("Deducted {Credits} credits from user {UserId} in Firestore. New balance: {NewBalance}", 
                    creditsToDeduct, userId, user.Credits);

                return true;
            }
            
            _logger.LogDebug("[Credits] Using SQLite/EF to deduct credits for user: {UserId}", userId);
            var efUser = await _context.Users.FindAsync(userId);
            if (efUser == null)
            {
                _logger.LogWarning("User {UserId} not found in SQLite for credit deduction", userId);
                return false;
            }

            if (efUser.Credits < creditsToDeduct)
            {
                _logger.LogWarning("User {UserId} has insufficient credits. Current: {Current}, Required: {Required}", 
                    userId, efUser.Credits, creditsToDeduct);
                return false;
            }

            efUser.Credits -= creditsToDeduct;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deducted {Credits} credits from user {UserId} in SQLite. New balance: {NewBalance}", 
                creditsToDeduct, userId, efUser.Credits);

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
            // Try Firestore first, fall back to EF
            if (await _firestore.IsAvailableAsync())
            {
                _logger.LogDebug("[Credits] Using Firestore to get credits for user: {UserId}", userId);
                var user = await _firestore.GetUserByIdAsync(userId);
                
                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                return user.Credits;
            }
            
            _logger.LogDebug("[Credits] Using SQLite/EF to get credits for user: {UserId}", userId);
            var efUser = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Credits)
                .FirstOrDefaultAsync();

            if (efUser == 0 && !await _context.Users.AnyAsync(u => u.Id == userId))
            {
                throw new UserNotFoundException(userId);
            }

            return efUser;
        }
        catch (UserNotFoundException)
        {
            throw;
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
            // Try Firestore first, fall back to EF
            if (await _firestore.IsAvailableAsync())
            {
                _logger.LogDebug("[Credits] Using Firestore to add credits for user: {UserId}", userId);
                var user = await _firestore.GetUserByIdAsync(userId);
                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                user.Credits += creditsToAdd;
                await _firestore.UpdateUserAsync(user);

                _logger.LogInformation("Added {Credits} credits to user {UserId} in Firestore. New balance: {NewBalance}", 
                    creditsToAdd, userId, user.Credits);

                return true;
            }
            
            _logger.LogDebug("[Credits] Using SQLite/EF to add credits for user: {UserId}", userId);
            var efUser = await _context.Users.FindAsync(userId);
            if (efUser == null)
            {
                throw new UserNotFoundException(userId);
            }

            efUser.Credits += creditsToAdd;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added {Credits} credits to user {UserId} in SQLite. New balance: {NewBalance}", 
                creditsToAdd, userId, efUser.Credits);

            return true;
        }
        catch (UserNotFoundException)
        {
            throw;
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
