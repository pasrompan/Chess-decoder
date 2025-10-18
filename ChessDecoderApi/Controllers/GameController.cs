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
}

