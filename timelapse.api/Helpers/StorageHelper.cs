using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace timelapse.api.Helpers
{
    class StorageHelper{

        private IConfiguration config;
        private ILogger _logger { get; }
        private readonly IMemoryCache _memoryCache;

        private Azure.Storage.Blobs.BlobServiceClient blobServiceClient = null;
        private Azure.Storage.Blobs.BlobContainerClient blobContainerClient = null;
        private string azureStorageConnectionString = null;
        private string azureBlobContainerName = null;

        // public string SasUri {get; private set;}

        public StorageHelper(IConfiguration configuration, ILogger logger, IMemoryCache memoryCache){
            config = configuration;
            _logger = logger;
            _memoryCache = memoryCache;

            azureStorageConnectionString = config["STORAGE_CONNECTION_STRING"]; // Spent a while trying to figure out why this wasn't working, turns out that dotnet cli has weird behaviour with dotnet watch run running from the parent directory, solutions involve passing `--foo bar` as an argument or `--configuration appsettings.json`
            azureBlobContainerName = config["STORAGE_CONTAINER_NAME"];

            blobContainerClient = new Azure.Storage.Blobs.BlobContainerClient(azureStorageConnectionString, azureBlobContainerName);
        }

        public bool Download(string blobName, string localFilePath){
            try{
                _logger.LogDebug($"Download(\"{blobName}\")");
                Azure.Storage.Blobs.Models.BlobDownloadInfo blobDownloadInfo = blobContainerClient.GetBlobClient(blobName).Download();
                
                using(FileStream fileStream = File.OpenWrite(localFilePath)){
                    blobDownloadInfo.Content.CopyTo(fileStream);
                }

                return true;
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to access blob {blobName}");
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public Uri Upload(string blobName, Stream stream){
            try{
                _logger.LogDebug($"Upload(\"{blobName}\")");
                Azure.Storage.Blobs.BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
                var blobContentInfo = blobClient.Upload(stream, true);
                return blobClient.Uri;
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to access blob {blobName}");
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public bool Upload(string blobName, string localFilePath){
            try{
                _logger.LogDebug($"Upload(\"{blobName}\")");
                Azure.Storage.Blobs.BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
                using(FileStream uploadFileStream = File.OpenRead(localFilePath)){
                    blobClient.Upload(uploadFileStream, true);
                    uploadFileStream.Close();
                }
                return true;
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to access blob {blobName}");
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public Uri GenerateSasUri(){
            try{

                Uri sasUri;
                if(!_memoryCache.TryGetValue("SasUri", out sasUri)){
                    sasUri = blobContainerClient.GenerateSasUri(Azure.Storage.Sas.BlobContainerSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(48));
                    _memoryCache.Set("SasUri", sasUri, TimeSpan.FromHours(48));
                } 

                return sasUri;
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to GenerateSasUri");
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public string SasToken{
            get{
                return GenerateSasUri().Query;
            }
        }
    }
}