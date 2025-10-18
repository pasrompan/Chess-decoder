using Microsoft.AspNetCore.Mvc;
using ChessDecoderApi.Services;
using ChessDecoderApi.Models;

namespace ChessDecoderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FirestoreTestController : ControllerBase
{
    private readonly IFirestoreService _firestore;
    private readonly ILogger<FirestoreTestController> _logger;

    public FirestoreTestController(IFirestoreService firestore, ILogger<FirestoreTestController> logger)
    {
        _firestore = firestore;
        _logger = logger;
    }

    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        try
        {
            // Test 1: Check if Firestore is available
            var isAvailable = await _firestore.IsAvailableAsync();
            if (!isAvailable)
            {
                return Ok(new { success = false, message = "Firestore is not available" });
            }

            // Test 2: Create a test user
            var testUser = new User
            {
                Id = "test_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Email = $"test_{DateTime.UtcNow.Ticks}@example.com",
                Name = "Test User",
                Provider = "test",
                Credits = 10
            };

            var createdUser = await _firestore.CreateUserAsync(testUser);
            _logger.LogInformation("[Firestore Test] Created user: {UserId}", createdUser.Id);

            // Test 3: Read the user back
            var readUser = await _firestore.GetUserByIdAsync(createdUser.Id);
            
            // Test 4: Delete the test user
            await _firestore.DeleteUserAsync(createdUser.Id);

            return Ok(new
            {
                success = true,
                message = "Firestore is working!",
                firestoreAvailable = isAvailable,
                testResults = new
                {
                    created = createdUser != null,
                    read = readUser != null,
                    deleted = true,
                    userId = createdUser.Id,
                    userEmail = createdUser.Email
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firestore Test] Error testing Firestore");
            return Ok(new
            {
                success = false,
                message = "Firestore test failed",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var isAvailable = await _firestore.IsAvailableAsync();
        return Ok(new
        {
            firestoreAvailable = isAvailable,
            message = isAvailable ? "Firestore is connected" : "Firestore is not available"
        });
    }
}


