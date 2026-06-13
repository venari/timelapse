using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace timelapse.functions.services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly string _containerName;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        var connectionString = configuration["STORAGE_CONNECTION_STRING"];
        logger.LogInformation("Initializing BlobStorageService with connection string: {ConnectionString}", connectionString);

        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerName = configuration["STORAGE_CONTAINER_NAME"] ?? "timelapse";

        _logger = logger;
        _logger.LogInformation($"Initializing StorageHelper with connection string: {connectionString} and container name: {_containerName}");
    }

    public async Task DownloadFileAsync(string blobPath, string localPath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);
            
            await blobClient.DownloadToAsync(localPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download blob {BlobPath} to {LocalPath}", blobPath, localPath);
            throw;
        }
    }

    public async Task<string> UploadVideoAsync(string localPath, string blobPath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("timelapses");
            await containerClient.CreateIfNotExistsAsync();
            
            var blobClient = containerClient.GetBlobClient(blobPath);
            
            await blobClient.UploadAsync(localPath, overwrite: true);
            
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload video {LocalPath} to {BlobPath}", localPath, blobPath);
            throw;
        }
    }
}
