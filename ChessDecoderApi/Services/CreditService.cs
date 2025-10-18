using ChessDecoderApi.Models;
using ChessDecoderApi.Exceptions;
using ChessDecoderApi.Repositories;

namespace ChessDecoderApi.Services;

public class CreditService : ICreditService
{
    private readonly RepositoryFactory _repositoryFactory;
    private readonly ILogger<CreditService> _logger;

    public CreditService(RepositoryFactory repositoryFactory, ILogger<CreditService> logger)
    {
        _repositoryFactory = repositoryFactory;
        _logger = logger;
    }

    public async Task<bool> HasEnoughCreditsAsync(string userId, int requiredCredits = 1)
    {
        try
        {
            var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
            var credits = await userRepo.GetCreditsAsync(userId);
            return credits >= requiredCredits;
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
            var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
            var user = await userRepo.GetByIdAsync(userId);
            
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
            await userRepo.UpdateAsync(user);

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
            var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
            var credits = await userRepo.GetCreditsAsync(userId);
            
            if (credits == 0 && !await userRepo.ExistsAsync(userId))
            {
                throw new UserNotFoundException(userId);
            }

            return credits;
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
            var userRepo = await _repositoryFactory.CreateUserRepositoryAsync();
            var user = await userRepo.GetByIdAsync(userId);
            
            if (user == null)
            {
                throw new UserNotFoundException(userId);
            }

            user.Credits += creditsToAdd;
            await userRepo.UpdateAsync(user);

            _logger.LogInformation("Added {Credits} credits to user {UserId}. New balance: {NewBalance}", 
                creditsToAdd, userId, user.Credits);

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
