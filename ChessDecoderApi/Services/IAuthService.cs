using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services;

public interface IAuthService
{
    Task<AuthResponse> VerifyGoogleTokenAsync(string accessToken);
    Task<User?> GetUserProfileAsync(string userId);
    Task<User> UpdateUserProfileAsync(string userId, UserProfileRequest request);
    Task<bool> SignOutUserAsync(string userId);
}
