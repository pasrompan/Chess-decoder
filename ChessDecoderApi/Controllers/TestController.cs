using ChessDecoderApi.Data;
using ChessDecoderApi.Models;
using ChessDecoderApi.Services;
using ChessDecoderApi.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChessDecoderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ChessDecoderDbContext _context;
    private readonly ICreditService _creditService;
    private readonly ILogger<TestController> _logger;

    public TestController(ChessDecoderDbContext context, ICreditService creditService, ILogger<TestController> logger)
    {
        _context = context;
        _creditService = creditService;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            // Test database connection
            await _context.Database.CanConnectAsync();
            
            return Ok(new
            {
                status = "healthy",
                database = "connected",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return StatusCode(500, new
            {
                status = "unhealthy",
                database = "disconnected",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Name,
                    u.Credits,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .ToListAsync();

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { error = "Failed to retrieve users" });
        }
    }

    [HttpGet("users/{userId}/credits")]
    public async Task<IActionResult> GetUserCredits(string userId)
    {
        try
        {
            var credits = await _creditService.GetUserCreditsAsync(userId);
            return Ok(new { userId, credits });
        }
        catch (UserNotFoundException ex)
        {
            _logger.LogWarning("User {UserId} not found when retrieving credits", ex.UserId);
            return NotFound(new { error = "User not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving credits for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve user credits" });
        }
    }

    [HttpPost("users/{userId}/credits/add")]
    public async Task<IActionResult> AddCredits(string userId, [FromBody] AddCreditsRequest request)
    {
        try
        {
            var success = await _creditService.AddCreditsAsync(userId, request.Credits);
            if (success)
            {
                var newBalance = await _creditService.GetUserCreditsAsync(userId);
                return Ok(new { userId, creditsAdded = request.Credits, newBalance });
            }
            
            return BadRequest(new { error = "Failed to add credits" });
        }
        catch (UserNotFoundException ex)
        {
            _logger.LogWarning("User {UserId} not found when adding credits", ex.UserId);
            return NotFound(new { error = "User not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding credits for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to add credits" });
        }
    }

    [HttpGet("games")]
    public async Task<IActionResult> GetGames()
    {
        try
        {
            var games = await _context.ChessGames
                .Include(g => g.User)
                .Include(g => g.InputImages)
                .Include(g => g.Statistics)
                .Select(g => new
                {
                    g.Id,
                    g.Title,
                    g.Description,
                    g.ProcessedAt,
                    g.ProcessingTimeMs,
                    g.IsValid,
                    User = new { g.User.Id, g.User.Name, g.User.Email },
                    ImageCount = g.InputImages.Count,
                    Statistics = g.Statistics != null ? new
                    {
                        g.Statistics.TotalMoves,
                        g.Statistics.ValidMoves,
                        g.Statistics.InvalidMoves,
                        g.Statistics.Opening,
                        g.Statistics.Result
                    } : null
                })
                .ToListAsync();

            return Ok(games);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving games");
            return StatusCode(500, new { error = "Failed to retrieve games" });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = new
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalGames = await _context.ChessGames.CountAsync(),
                TotalImages = await _context.GameImages.CountAsync(),
                AverageProcessingTime = await _context.ChessGames.AverageAsync(g => g.ProcessingTimeMs),
                ValidGames = await _context.ChessGames.CountAsync(g => g.IsValid),
                InvalidGames = await _context.ChessGames.CountAsync(g => !g.IsValid)
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    [HttpPost("test-data")]
    public async Task<IActionResult> CreateTestData()
    {
        try
        {
            // Check if test data already exists
            if (await _context.Users.AnyAsync())
            {
                return BadRequest(new { message = "Test data already exists" });
            }

            // Create a test user
            var testUser = new User
            {
                Id = "test-user-001",
                Email = "test@chessdecoder.com",
                Name = "Test User",
                Picture = "https://via.placeholder.com/150",
                Provider = "google",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                LastLoginAt = DateTime.UtcNow,
                Credits = 25
            };

            _context.Users.Add(testUser);

            // Create a test chess game
            var testGame = new ChessGame
            {
                Id = Guid.NewGuid(),
                UserId = testUser.Id,
                Title = "Test Chess Game - Ruy Lopez Opening",
                Description = "A sample chess game for testing purposes",
                PgnContent = "1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 5. O-O Be7 6. Re1 b5 7. Bb3 d6 8. c3 O-O 9. h3 Nb8 10. d4 Nbd7",
                ProcessedAt = DateTime.UtcNow.AddHours(-2),
                ProcessingTimeMs = 1250,
                IsValid = true
            };

            _context.ChessGames.Add(testGame);

            // Create test game images
            var testImage1 = new GameImage
            {
                Id = Guid.NewGuid(),
                ChessGameId = testGame.Id,
                FileName = "test-game-1.jpg",
                FilePath = "/uploads/test-game-1.jpg",
                UploadedAt = DateTime.UtcNow.AddHours(-2)
            };

            var testImage2 = new GameImage
            {
                Id = Guid.NewGuid(),
                ChessGameId = testGame.Id,
                FileName = "test-game-2.jpg",
                FilePath = "/uploads/test-game-2.jpg",
                UploadedAt = DateTime.UtcNow.AddHours(-2)
            };

            _context.GameImages.AddRange(testImage1, testImage2);

            // Create game statistics
            var testStats = new GameStatistics
            {
                Id = Guid.NewGuid(),
                ChessGameId = testGame.Id,
                TotalMoves = 10,
                ValidMoves = 10,
                InvalidMoves = 0,
                Opening = "Ruy Lopez",
                Result = "In Progress"
            };

            _context.GameStatistics.Add(testStats);

            // Save all changes
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Test data created successfully",
                user = new { testUser.Id, testUser.Email, testUser.Name, testUser.Credits },
                game = new { testGame.Id, testGame.Title, testGame.IsValid },
                images = 2,
                statistics = new { testStats.TotalMoves, testStats.Opening }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test data");
            return StatusCode(500, new { error = "Failed to create test data", details = ex.Message });
        }
    }
}

public class AddCreditsRequest
{
    public int Credits { get; set; }
}
