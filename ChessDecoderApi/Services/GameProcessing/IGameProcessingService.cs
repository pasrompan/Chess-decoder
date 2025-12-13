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
}

