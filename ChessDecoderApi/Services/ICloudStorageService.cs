using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services;

public interface ICloudStorageService
{
    Task<bool> SyncDatabaseToCloudAsync();
    Task<bool> SyncDatabaseFromCloudAsync();
    Task<string> UploadGameImageAsync(Stream imageStream, string fileName, string contentType);
    Task<bool> DeleteGameImageAsync(string fileName);
    Task<Stream> DownloadGameImageAsync(string fileName);
    Task<string> GetImageUrlAsync(string fileName);
}
