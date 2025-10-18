using ChessDecoderApi.Models;

namespace ChessDecoderApi.Services;

public interface ICloudStorageService
{
    // Image storage operations
    Task<string> UploadGameImageAsync(Stream imageStream, string fileName, string contentType);
    Task<bool> DeleteGameImageAsync(string fileName);
    Task<Stream> DownloadGameImageAsync(string fileName);
    Task<string> GetImageUrlAsync(string fileName);
    Task<bool> DeleteGameImageByObjectNameAsync(string objectName);
}
