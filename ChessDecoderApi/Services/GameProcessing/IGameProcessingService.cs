using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for handling the full game processing workflow
/// </summary>
public interface IGameProcessingService
{
    /// <summary>
    /// Process an uploaded chess game image including credit check, processing, and saving
    /// </summary>
    Task<GameProcessingResponse> ProcessGameUploadAsync(GameUploadRequest request);

    /// <summary>
    /// Process a mock upload (no credit deduction, no database save, language auto-detected)
    /// </summary>
    Task<GameProcessingResponse> ProcessMockUploadAsync(IFormFile image, bool autoCrop = false);

    /// <summary>
    /// Process two uploaded pages and merge into a single game.
    /// </summary>
    Task<DualGameUploadResponse> ProcessDualGameUploadAsync(DualGameUploadRequest request);

    /// <summary>
    /// Add a continuation page to an existing game.
    /// </summary>
    Task<ContinuationUploadResponse> AddContinuationAsync(Guid gameId, ContinuationUploadRequest request);

    /// <summary>
    /// Return page metadata for a game.
    /// </summary>
    Task<GamePagesResponse> GetGamePagesAsync(Guid gameId, string userId);

    /// <summary>
    /// Remove continuation page from a game and restore page-1-only PGN.
    /// </summary>
    Task<DeleteContinuationResponse> DeleteContinuationAsync(Guid gameId, string userId);
}
