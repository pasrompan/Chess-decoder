namespace ChessDecoderApi.Services;

public interface ICreditService
{
    Task<bool> HasEnoughCreditsAsync(string userId, int requiredCredits = 1);
    Task<bool> DeductCreditsAsync(string userId, int creditsToDeduct = 1);
    Task<int> GetUserCreditsAsync(string userId);
    Task<bool> AddCreditsAsync(string userId, int creditsToAdd);
    Task<bool> RefundCreditsAsync(string userId, int creditsToRefund);
}
