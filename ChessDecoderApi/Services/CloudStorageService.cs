using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services;

public class CloudStorageService : ICloudStorageService
{
    private readonly StorageClient _storageClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudStorageService> _logger;
    private readonly string _databaseBucketName;
    private readonly string _imagesBucketName;
    private readonly string _databaseFileName;

    public CloudStorageService(IConfiguration configuration, ILogger<CloudStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Get bucket names from environment variables first, then configuration
        _databaseBucketName = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_DATABASE_BUCKET") 
            ?? _configuration["CloudStorage:DatabaseBucketName"] 
            ?? "chessdecoder-db";
        _imagesBucketName = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_IMAGES_BUCKET") 
            ?? _configuration["CloudStorage:ImagesBucketName"] 
            ?? "chessdecoder-images";
        _databaseFileName = "chessdecoder.db";
        
        // Try to initialize Google Cloud Storage client
        // In Cloud Run, credentials are automatically available via the default service account
        try
        {
            _storageClient = StorageClient.Create();
            _logger.LogInformation("Google Cloud Storage client initialized successfully with buckets: {DatabaseBucket}, {ImagesBucket}", 
                _databaseBucketName, _imagesBucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Google Cloud Storage client. Running in local-only mode.");
            _logger.LogWarning("To enable cloud storage:");
            _logger.LogWarning("1. Run 'gcloud auth application-default login' for local development");
            _logger.LogWarning("2. Or set GOOGLE_APPLICATION_CREDENTIALS environment variable to your service account key file");
            _logger.LogWarning("3. Or ensure Cloud Run service account has proper permissions");
            _storageClient = null!;
        }
    }

    public async Task<bool> SyncDatabaseToCloudAsync()
    {
        if (_storageClient == null) return false;

        try
        {
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "data", _databaseFileName);
            if (!File.Exists(dbPath)) return false;

            using var stream = File.OpenRead(dbPath);
            await _storageClient.UploadObjectAsync(_databaseBucketName, _databaseFileName, "application/x-sqlite3", stream);
            
            _logger.LogInformation("Database synced to Cloud Storage: {Bucket}/{FileName}", _databaseBucketName, _databaseFileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync database to Cloud Storage");
            return false;
        }
    }

    public async Task<bool> SyncDatabaseFromCloudAsync()
    {
        if (_storageClient == null) return false;

        try
        {
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "data", _databaseFileName);
            var dbDir = Path.GetDirectoryName(dbPath);
            
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir!);
            }

            // Check if the object exists in the bucket first
            try
            {
                await _storageClient.GetObjectAsync(_databaseBucketName, _databaseFileName);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("No database found in Cloud Storage bucket {Bucket}. Will create a new local database.", _databaseBucketName);
                return false; // This is not an error - just no cloud database exists yet
            }

            using var stream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(_databaseBucketName, _databaseFileName, stream);
            
            stream.Position = 0;
            using var fileStream = File.Create(dbPath);
            await stream.CopyToAsync(fileStream);
            
            _logger.LogInformation("Database synced from Cloud Storage: {Bucket}/{FileName}", _databaseBucketName, _databaseFileName);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("No database found in Cloud Storage bucket {Bucket}. Will create a new local database.", _databaseBucketName);
            return false; // This is not an error - just no cloud database exists yet
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync database from Cloud Storage. Using local database if available.");
            return false;
        }
    }

    public async Task<string> UploadGameImageAsync(Stream imageStream, string fileName, string contentType)
    {
        if (_storageClient == null) throw new InvalidOperationException("Cloud Storage not available");

        try
        {
            var objectName = $"game-images/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";
            await _storageClient.UploadObjectAsync(_imagesBucketName, objectName, contentType, imageStream);
            
            // Note: Bucket should be configured as public during setup
            _logger.LogInformation("Image uploaded to Cloud Storage bucket (bucket should be public): {Bucket}/{ObjectName}", _imagesBucketName, objectName);
            
            _logger.LogInformation("Game image uploaded to Cloud Storage: {Bucket}/{ObjectName}", _imagesBucketName, objectName);
            return objectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload game image to Cloud Storage");
            throw;
        }
    }

    public async Task<bool> DeleteGameImageAsync(string fileName)
    {
        if (_storageClient == null) return false;

        try
        {
            await _storageClient.DeleteObjectAsync(_imagesBucketName, fileName);
            _logger.LogInformation("Game image deleted from Cloud Storage: {Bucket}/{FileName}", _imagesBucketName, fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete game image from Cloud Storage");
            return false;
        }
    }

    public async Task<Stream> DownloadGameImageAsync(string fileName)
    {
        if (_storageClient == null) throw new InvalidOperationException("Cloud Storage not available");

        try
        {
            var stream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(_imagesBucketName, fileName, stream);
            stream.Position = 0;
            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download game image from Cloud Storage");
            throw;
        }
    }

    public Task<string> GetImageUrlAsync(string fileName)
    {
        if (_storageClient == null) return Task.FromResult(string.Empty);

        try
        {
            var url = $"https://storage.googleapis.com/{_imagesBucketName}/{fileName}";
            _logger.LogInformation("Generated image URL: {Url}", url);
            return Task.FromResult(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image URL");
            return Task.FromResult(string.Empty);
        }
    }

    public async Task<bool> DeleteGameImageByObjectNameAsync(string objectName)
    {
        if (_storageClient == null) return false;

        try
        {
            await _storageClient.DeleteObjectAsync(_imagesBucketName, objectName);
            _logger.LogInformation("Game image deleted from Cloud Storage: {Bucket}/{ObjectName}", _imagesBucketName, objectName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete game image from Cloud Storage: {ObjectName}", objectName);
            return false;
        }
    }
}
