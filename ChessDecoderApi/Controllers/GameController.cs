using ChessDecoderApi.DTOs.Requests;
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdatePgn(Guid gameId, [FromBody] UpdatePgnRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(new { message = "UserId is required" });
            }

            if (string.IsNullOrWhiteSpace(request.PgnContent))
            {
                return BadRequest(new { message = "PGN content cannot be empty" });
            }

            var result = await _gameManagementService.UpdatePgnContentAsync(gameId, request.UserId, request.PgnContent);
            
            if (result == null)
            {
                return NotFound(new { message = "Game not found" });
            }
            
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to update game {GameId}", gameId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for game {GameId}: {Message}", gameId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating PGN for game {GameId}", gameId);
            return StatusCode(500, new { message = "Failed to update PGN content" });
        }
    }

    /// <summary>
    /// Mark processing as completed for a game
    /// </summary>
    [HttpPut("{gameId}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MarkProcessingComplete(Guid gameId, [FromBody] CompleteProcessingRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(new { message = "UserId is required" });
            }

            var result = await _gameManagementService.MarkProcessingCompleteAsync(gameId, request.UserId);
            
            if (result == null)
            {
                return NotFound(new { message = "Game not found" });
            }
            
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to mark game {GameId} as complete", gameId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking game {GameId} as complete", gameId);
            return StatusCode(500, new { message = "Failed to mark processing as complete" });
        }
    }
}

