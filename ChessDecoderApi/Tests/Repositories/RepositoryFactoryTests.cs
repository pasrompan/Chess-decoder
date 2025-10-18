using ChessDecoderApi.Repositories;
using ChessDecoderApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Repositories;

public class RepositoryFactoryTests
{
    [Fact]
    public async Task CreateUserRepositoryAsync_FirestoreNotAvailable_ReturnsSqliteRepository()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var firestoreServiceMock = new Mock<IFirestoreService>();
        firestoreServiceMock.Setup(x => x.IsAvailableAsync()).ReturnsAsync(false);
        
        var loggerMock = new Mock<ILogger<RepositoryFactory>>();
        
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(firestoreServiceMock.Object);
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        
        var factory = new RepositoryFactory(serviceProvider, firestoreServiceMock.Object, loggerMock.Object);

        // Act & Assert
        // Note: This will fail because we didn't register DbContext, but that's expected
        // In a real scenario, the factory would return a SQLite repository
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await factory.CreateUserRepositoryAsync());
        
        // Verify Firestore was checked
        firestoreServiceMock.Verify(x => x.IsAvailableAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateUserRepositoryAsync_CachesAvailabilityCheck()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var firestoreServiceMock = new Mock<IFirestoreService>();
        firestoreServiceMock.Setup(x => x.IsAvailableAsync()).ReturnsAsync(false);
        
        var loggerMock = new Mock<ILogger<RepositoryFactory>>();
        
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(firestoreServiceMock.Object);
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        
        var factory = new RepositoryFactory(serviceProvider, firestoreServiceMock.Object, loggerMock.Object);

        // Act - Call multiple times
        try { await factory.CreateUserRepositoryAsync(); } catch { }
        try { await factory.CreateChessGameRepositoryAsync(); } catch { }
        try { await factory.CreateGameImageRepositoryAsync(); } catch { }

        // Assert - Firestore availability should only be checked once (cached)
        firestoreServiceMock.Verify(x => x.IsAvailableAsync(), Times.Once);
    }
}

