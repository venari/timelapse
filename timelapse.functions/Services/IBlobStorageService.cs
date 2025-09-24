namespace timelapse.functions.services;

public interface IBlobStorageService
{
    Task DownloadFileAsync(string blobPath, string localPath);
    Task<string> UploadVideoAsync(string localPath, string blobPath);
}