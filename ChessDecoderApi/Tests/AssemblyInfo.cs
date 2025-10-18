using Xunit;

// Run all tests sequentially to avoid database concurrency issues with in-memory SQLite
// MaxParallelThreads = 1 forces all tests to run on a single thread
[assembly: CollectionBehavior(MaxParallelThreads = 1)]

