using Google.Cloud.Firestore;
using ChessDecoderApi.Data;
using ChessDecoderApi.Repositories.Interfaces;
using ChessDecoderApi.Repositories.Firestore;
using ChessDecoderApi.Repositories.Sqlite;
using ChessDecoderApi.Services;

namespace ChessDecoderApi.Repositories;

/// <summary>
/// Factory for creating repository implementations based on database availability
/// Prioritizes Firestore, falls back to SQLite
/// </summary>
public class RepositoryFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFirestoreService _firestoreService;
    private readonly ILogger<RepositoryFactory> _logger;
    private bool? _isFirestoreAvailable;

    public RepositoryFactory(
        IServiceProvider serviceProvider,
        IFirestoreService firestoreService,
        ILogger<RepositoryFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _firestoreService = firestoreService;
        _logger = logger;
    }

    private async Task<bool> IsFirestoreAvailableAsync()
    {
        // Cache the result to avoid multiple checks
        if (_isFirestoreAvailable.HasValue)
        {
            return _isFirestoreAvailable.Value;
        }

        _isFirestoreAvailable = await _firestoreService.IsAvailableAsync();
        
        if (_isFirestoreAvailable.Value)
        {
            _logger.LogInformation("[RepositoryFactory] Using Firestore repositories");
        }
        else
        {
            _logger.LogInformation("[RepositoryFactory] Firestore not available, using SQLite repositories");
        }
        
        return _isFirestoreAvailable.Value;
    }

    public virtual async Task<IUserRepository> CreateUserRepositoryAsync()
    {
        if (await IsFirestoreAvailableAsync())
        {
            var firestoreDb = _serviceProvider.GetService<FirestoreDb>();
            if (firestoreDb != null)
            {
                var logger = _serviceProvider.GetRequiredService<ILogger<FirestoreUserRepository>>();
                return new FirestoreUserRepository(firestoreDb, logger);
            }
        }

        var context = _serviceProvider.GetRequiredService<ChessDecoderDbContext>();
        var sqliteLogger = _serviceProvider.GetRequiredService<ILogger<SqliteUserRepository>>();
        return new SqliteUserRepository(context, sqliteLogger);
    }

    public virtual async Task<IChessGameRepository> CreateChessGameRepositoryAsync()
    {
        if (await IsFirestoreAvailableAsync())
        {
            var firestoreDb = _serviceProvider.GetService<FirestoreDb>();
            if (firestoreDb != null)
            {
                var logger = _serviceProvider.GetRequiredService<ILogger<FirestoreChessGameRepository>>();
                return new FirestoreChessGameRepository(firestoreDb, logger);
            }
        }

        var context = _serviceProvider.GetRequiredService<ChessDecoderDbContext>();
        var sqliteLogger = _serviceProvider.GetRequiredService<ILogger<SqliteChessGameRepository>>();
        return new SqliteChessGameRepository(context, sqliteLogger);
    }

    public virtual async Task<IGameImageRepository> CreateGameImageRepositoryAsync()
    {
        if (await IsFirestoreAvailableAsync())
        {
            var firestoreDb = _serviceProvider.GetService<FirestoreDb>();
            if (firestoreDb != null)
            {
                var logger = _serviceProvider.GetRequiredService<ILogger<FirestoreGameImageRepository>>();
                return new FirestoreGameImageRepository(firestoreDb, logger);
            }
        }

        var context = _serviceProvider.GetRequiredService<ChessDecoderDbContext>();
        var sqliteLogger = _serviceProvider.GetRequiredService<ILogger<SqliteGameImageRepository>>();
        return new SqliteGameImageRepository(context, sqliteLogger);
    }

    public virtual async Task<IGameStatisticsRepository> CreateGameStatisticsRepositoryAsync()
    {
        if (await IsFirestoreAvailableAsync())
        {
            var firestoreDb = _serviceProvider.GetService<FirestoreDb>();
            if (firestoreDb != null)
            {
                var logger = _serviceProvider.GetRequiredService<ILogger<FirestoreGameStatisticsRepository>>();
                return new FirestoreGameStatisticsRepository(firestoreDb, logger);
            }
        }

        var context = _serviceProvider.GetRequiredService<ChessDecoderDbContext>();
        var sqliteLogger = _serviceProvider.GetRequiredService<ILogger<SqliteGameStatisticsRepository>>();
        return new SqliteGameStatisticsRepository(context, sqliteLogger);
    }
}

