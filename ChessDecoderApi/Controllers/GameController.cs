using ChessDecoderApi.DTOs.Requests;
using ChessDecoderApi.DTOs.Responses;
using ChessDecoderApi.Services.GameProcessing;
using Microsoft.AspNetCore.Mvc;

namespace ChessDecoderApi.Controllers;

/// <summary>
/// Controller for core chess game operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly IGameProcessingService _gameProcessingService;
    private readonly IGameManagementService _gameManagementService;
    private readonly ILogger<GameController> _logger;

    public GameController(
        IGameProcessingService gameProcessingService,
        IGameManagementService gameManagementService,
        ILogger<GameController> logger)
    {
        _gameProcessingService = gameProcessingService ?? throw new ArgumentNullException(nameof(gameProcessingService));
        _gameManagementService = gameManagementService ?? throw new ArgumentNullException(nameof(gameManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CheckHealth()
    {
        return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Upload and process a chess game image
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadGame([FromForm] GameUploadRequest request)
    {
        try
        {
            var response = await _gameProcessingService.ProcessGameUploadAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during game upload for user {UserId}", request.UserId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing game upload for user {UserId}", request.UserId);
            return StatusCode(500, new { message = "Failed to process image" });
        }
    }

    /// <summary>
    /// Upload and process two chess scoresheet pages as one game.
    /// </summary>
    [HttpPost("upload-dual")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDualGame([FromForm] DualGameUploadRequest request)
    {
        try
        {
            var response = await _gameProcessingService.ProcessDualGameUploadAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during dual game upload for user {UserId}", request.UserId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing dual game upload for user {UserId}", request.UserId);
            return StatusCode(500, new { message = "Failed to process dual image upload" });
        }
    }

    /// <summary>
    /// Add a continuation page to an existing game.
    /// </summary>
    [HttpPost("{gameId}/continuation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadContinuation(Guid gameId, [FromForm] ContinuationUploadRequest request)
    {
        try
        {
            var response = await _gameProcessingService.AddContinuationAsync(gameId, request);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid continuation upload for game {GameId}", gameId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading continuation for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to upload continuation image" });
        }
    }

    /// <summary>
    /// Get page metadata for a game.
    /// </summary>
    [HttpGet("{gameId}/pages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetGamePages(Guid gameId, [FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = "UserId is required" });
            }

            var response = await _gameProcessingService.GetGamePagesAsync(gameId, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pages for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to retrieve game pages" });
        }
    }

    /// <summary>
    /// Delete continuation page from a game and restore page 1 PGN.
    /// </summary>
    [HttpDelete("{gameId}/continuation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteContinuation(Guid gameId, [FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = "UserId is required" });
            }

            var response = await _gameProcessingService.DeleteContinuationAsync(gameId, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting continuation for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to delete continuation" });
        }
    }

    /// <summary>
    /// Get a chess game by ID
    /// </summary>
    [HttpGet("{gameId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGame(Guid gameId)
    {
        try
        {
            var game = await _gameManagementService.GetGameByIdAsync(gameId);
            
            if (game == null)
            {
                return NotFound(new { message = "Game not found" });
            }
            
            return Ok(game);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to retrieve game" });
        }
    }

    /// <summary>
    /// Get all games for a user
    /// </summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserGames(string userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var games = await _gameManagementService.GetUserGamesAsync(userId, pageNumber, pageSize);
            return Ok(games);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving games for user {UserId}", userId);
            return StatusCode(500, new { message = "Failed to retrieve games" });
        }
    }

    /// <summary>
    /// Update game metadata (player details, date, round)
    /// </summary>
    [HttpPut("{gameId}/metadata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateGameMetadata(Guid gameId, [FromBody] UpdateGameMetadataRequest request)
    {
        try
        {
            var result = await _gameManagementService.UpdateGameMetadataAsync(gameId, request);
            
            if (!result)
            {
                return NotFound(new { message = "Game not found" });
            }
            
            return Ok(new { message = "Game metadata updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metadata for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to update game metadata" });
        }
    }

    /// <summary>
    /// Delete a chess game
    /// </summary>
    [HttpDelete("{gameId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGame(Guid gameId)
    {
        try
        {
            var result = await _gameManagementService.DeleteGameAsync(gameId);
            
            if (!result)
            {
                return NotFound(new { message = "Game not found" });
            }
            
            return Ok(new { message = "Game deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to delete game" });
        }
    }

    /// <summary>
    /// Update PGN content for a game
    /// </summary>
    [HttpPut("{gameId}/pgn")]
    [ProducesResponseType(typeof(GameDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateGamePgn(Guid gameId, [FromBody] UpdatePgnRequest request, [FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = "UserId is required" });
            }

            if (string.IsNullOrWhiteSpace(request.PgnContent))
            {
                return BadRequest(new { message = "PGN content cannot be empty" });
            }

            var result = await _gameManagementService.UpdatePgnContentAsync(gameId, userId, request.PgnContent);
            
            if (result == null)
            {
                return NotFound(new { message = "Game not found or access denied" });
            }
            
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating PGN for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to update game PGN" });
        }
    }

    /// <summary>
    /// Mark a game's processing as completed (user exported to Lichess/Chess.com)
    /// </summary>
    [HttpPut("{gameId}/complete")]
    [ProducesResponseType(typeof(GameDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MarkProcessingComplete(Guid gameId, [FromQuery] string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = "UserId is required" });
            }

            var result = await _gameManagementService.MarkProcessingCompleteAsync(gameId, userId);
            
            if (result == null)
            {
                return NotFound(new { message = "Game not found or access denied" });
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking game {GameId} as complete", gameId);
            return StatusCode(500, new { message = "Failed to mark game as complete" });
        }
    }

    /// <summary>
    /// Get a game image by variant (processed/original) for a specific user.
    /// Falls back to original if requested variant is unavailable.
    /// </summary>
    [HttpGet("{gameId}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetGameImage(Guid gameId, [FromQuery] string userId, [FromQuery] string variant = "processed")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = "UserId is required" });
            }

            var imageResult = await _gameManagementService.GetGameImageAsync(gameId, userId, variant);
            if (imageResult == null)
            {
                return NotFound(new { message = "Game image not found or access denied" });
            }

            return File(imageResult.Stream, imageResult.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to retrieve game image" });
        }
    }
}
