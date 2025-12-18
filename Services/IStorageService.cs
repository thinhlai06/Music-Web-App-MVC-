namespace MusicWeb.Services;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder, string? subFolder = null);
    Task DeleteFileAsync(string fileUrl);
}
