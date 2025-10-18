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
    private readonly string _imagesBucketName;

    public CloudStorageService(IConfiguration configuration, ILogger<CloudStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Get image bucket name from environment variables first, then configuration
        var envImagesBucket = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_IMAGES_BUCKET");
        var configImagesBucket = _configuration["CloudStorage:ImagesBucketName"];
        
        _logger.LogInformation("Environment variable - GOOGLE_CLOUD_IMAGES_BUCKET: '{EnvImagesBucket}'", 
            envImagesBucket ?? "null");
        _logger.LogInformation("Configuration value - ImagesBucketName: '{ConfigImagesBucket}'", 
            configImagesBucket ?? "null");
        
        _imagesBucketName = envImagesBucket ?? configImagesBucket ?? "chessdecoder-images";
            
        // Validate bucket name
        if (string.IsNullOrWhiteSpace(_imagesBucketName))
        {
            _imagesBucketName = "chessdecoder-images";
        }
        
        _logger.LogInformation("Final image bucket name: '{ImagesBucket}'", _imagesBucketName);
        
        // Try to initialize Google Cloud Storage client
        // In Cloud Run, credentials are automatically available via the default service account
        try
        {
            _storageClient = StorageClient.Create();
            _logger.LogInformation("Google Cloud Storage client initialized successfully with images bucket: {ImagesBucket}", 
                _imagesBucketName);
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

    // Database sync methods removed - now using Firestore for permanent cloud database
    // Firestore provides a single, shared database accessible by all Cloud Run instances
    // No need to sync database files anymore!

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
