using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace timelapse.api.Helpers
{
    class StorageHelper{

        private IConfiguration config;
        private ILogger _logger { get; }

        private Azure.Storage.Blobs.BlobServiceClient blobServiceClient = null;
        private Azure.Storage.Blobs.BlobContainerClient blobContainerClient = null;
        private string azureStorageConnectionString = null;
        private string azureBlobContainerName = null;

        public StorageHelper(IConfiguration configuration, ILogger logger){
            config = configuration;
            _logger = logger;

            azureStorageConnectionString = config["STORAGE_CONNECTION_STRING"];
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
                var blobContentInfo = blobClient.Upload(stream, false);
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
                // Need to optimise to cache token.
                var sasUri = blobContainerClient.GenerateSasUri(Azure.Storage.Sas.BlobContainerSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
                return sasUri;
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to GenerateSasUri");
                _logger.LogError(ex.ToString());
                throw;
            }
        }
    }
}