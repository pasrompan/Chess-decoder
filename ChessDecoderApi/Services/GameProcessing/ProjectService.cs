using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories;
using ChessDecoderApi.Repositories.Interfaces;

namespace ChessDecoderApi.Services.GameProcessing;

/// <summary>
/// Service for managing project history and tracking
/// </summary>
public class ProjectService : IProjectService
{
    private readonly RepositoryFactory _repositoryFactory;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        RepositoryFactory repositoryFactory,
        ILogger<ProjectService> logger)
    {
        _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProjectHistory> CreateProjectAsync(Guid gameId, string userId, InitialUploadData uploadData, ProcessingData processingData)
    {
        try
        {
            var historyRepo = await _repositoryFactory.CreateProjectHistoryRepositoryAsync();
            
            var history = new ProjectHistory
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                InitialUpload = uploadData,
                Processing = processingData,
                Versions = new List<HistoryEntry>
                {
                    new HistoryEntry
                    {
                        Version = 1,
                        Timestamp = DateTime.UtcNow,
                        ChangeType = "initial_upload",
                        Description = "Initial image upload and processing"
                    }
                }
            };

            await historyRepo.CreateAsync(history);
            
            _logger.LogInformation("Created project history for game {GameId}", gameId);
            return history;
        }
        catch (NotSupportedException)
        {
            // Firestore not available, skip project history creation
            _logger.LogWarning("Skipping project history creation - Firestore not available");
            return new ProjectHistory { GameId = gameId, UserId = userId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project history for game {GameId}", gameId);
            throw;
        }
    }

    public async Task<ProjectHistory?> GetProjectByGameIdAsync(Guid gameId)
    {
        try
        {
            var historyRepo = await _repositoryFactory.CreateProjectHistoryRepositoryAsync();
            var result = await historyRepo.GetByGameIdAsync(gameId);
            
            // If project history doesn't exist, try to create it on-demand from game data
            if (result == null)
            {
                // Use repository directly to avoid circular dependency
                var gameRepo = await _repositoryFactory.CreateChessGameRepositoryAsync();
                var imageRepo = await _repositoryFactory.CreateGameImageRepositoryAsync();
                var game = await gameRepo.GetByIdAsync(gameId);
                
                if (game != null)
                {
                    _logger.LogInformation("Creating project history on-demand for existing game {GameId}", gameId);
                    
                    // Get game images
                    var images = await imageRepo.GetByChessGameIdAsync(gameId);
                    var firstImage = images.FirstOrDefault();
                    
                    // Create minimal project history from existing game data
                    var uploadData = firstImage != null
                        ? new InitialUploadData
                        {
                            FileName = firstImage.FileName ?? "unknown",
                            FileSize = firstImage.FileSizeBytes,
                            FileType = firstImage.FileType ?? "image/jpeg",
                            UploadedAt = firstImage.UploadedAt,
                            StorageLocation = firstImage.IsStoredInCloud ? "cloud" : "local",
                            StorageUrl = firstImage.CloudStorageUrl
                        }
                        : new InitialUploadData
                        {
                            FileName = "unknown",
                            FileSize = 0,
                            FileType = "image/jpeg",
                            UploadedAt = game.ProcessedAt,
                            StorageLocation = "local"
                        };

                    var processingData = new ProcessingData
                    {
                        ProcessedAt = game.ProcessedAt,
                        PgnContent = game.PgnContent ?? "",
                        ValidationStatus = game.IsValid ? "valid" : "invalid",
                        ProcessingTimeMs = game.ProcessingTimeMs
                    };

                    result = await CreateProjectAsync(gameId, game.UserId, uploadData, processingData);
                }
            }
            
            return result;
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("Project history not available - Firestore not configured");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project history for game {GameId}", gameId);
            throw;
        }
    }

    public async Task<List<ProjectHistory>> GetUserProjectsAsync(string userId)
    {
        try
        {
            var historyRepo = await _repositoryFactory.CreateProjectHistoryRepositoryAsync();
            return await historyRepo.GetByUserIdAsync(userId);
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("Project history not available - Firestore not configured");
            return new List<ProjectHistory>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ProjectHistory?> AddHistoryEntryAsync(Guid gameId, string changeType, string description, Dictionary<string, object>? changes = null)
    {
        try
        {
            var historyRepo = await _repositoryFactory.CreateProjectHistoryRepositoryAsync();
            var history = await historyRepo.GetByGameIdAsync(gameId);
            
            if (history == null)
            {
                _logger.LogWarning("Project history not found for game {GameId}", gameId);
                return null;
            }

            history.Versions ??= new List<HistoryEntry>();
            var nextVersion = history.Versions.Count > 0 
                ? history.Versions.Max(v => v.Version) + 1 
                : 1;

            history.Versions.Add(new HistoryEntry
            {
                Version = nextVersion,
                Timestamp = DateTime.UtcNow,
                ChangeType = changeType,
                Description = description,
                Changes = changes
            });

            await historyRepo.UpdateAsync(history);
            
            _logger.LogInformation("Added history entry (version {Version}) for game {GameId}", nextVersion, gameId);
            return history;
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("Cannot add history entry - Firestore not configured");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding history entry for game {GameId}", gameId);
            throw;
        }
    }

    public async Task<ProjectHistory?> UpdateProcessingDataAsync(Guid gameId, ProcessingData processingData)
    {
        try
        {
            var historyRepo = await _repositoryFactory.CreateProjectHistoryRepositoryAsync();
            var history = await historyRepo.GetByGameIdAsync(gameId);
            
            if (history == null)
            {
                _logger.LogWarning("Project history not found for game {GameId}", gameId);
                return null;
            }

            history.Processing = processingData;

            await historyRepo.UpdateAsync(history);
            
            _logger.LogInformation("Updated processing data for game {GameId}", gameId);
            return history;
        }
        catch (NotSupportedException)
        {
            _logger.LogWarning("Cannot update processing data - Firestore not configured");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating processing data for game {GameId}", gameId);
            throw;
        }
    }

    private const string MockUserId = "mock-user";

    public async Task EnsureProjectForMockResponseAsync(Guid gameId, string pgnContent)
    {
        try
        {
            var historyRepo = await _repositoryFactory.CreateProjectHistoryRepositoryAsync();
            var existing = await historyRepo.GetByGameIdAsync(gameId);
            if (existing != null)
            {
                _logger.LogDebug("Project already exists for mock game {GameId}, skipping creation", gameId);
                return;
            }

            var uploadData = new InitialUploadData
            {
                FileName = "mock-upload.jpg",
                FileSize = 0,
                FileType = "image/jpeg",
                UploadedAt = DateTime.UtcNow,
                StorageLocation = "local"
            };
            var processingData = new ProcessingData
            {
                ProcessedAt = DateTime.UtcNow,
                PgnContent = pgnContent ?? "",
                ValidationStatus = "valid",
                ProcessingTimeMs = 0
            };
            await CreateProjectAsync(gameId, MockUserId, uploadData, processingData);
            _logger.LogInformation("Created project history for mock game {GameId}", gameId);
        }
        catch (NotSupportedException)
        {
            _logger.LogDebug("Project history not available (Firestore not configured), mock project page will not be available");
        }
    }
}
