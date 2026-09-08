using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Azure.Storage.Blobs.Specialized;   // GetAppendBlobClient() is an extension method - needs this using to resolve even though everything else here is fully-qualified

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

        // Appends `text` to blobName, creating it as an append blob first if it doesn't exist yet.
        // Used by LogController.Post() for the ESP32 units' periodic log push - an append blob
        // (rather than Upload(), which always replaces the whole blob) means each device only ever
        // sends the bytes it hasn't sent before, not its whole growing log file every time.
        public bool AppendText(string blobName, string text){
            try{
                _logger.LogDebug($"AppendText(\"{blobName}\")");
                Azure.Storage.Blobs.Specialized.AppendBlobClient appendBlobClient = blobContainerClient.GetAppendBlobClient(blobName);
                appendBlobClient.CreateIfNotExists();

                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
                using(MemoryStream stream = new MemoryStream(bytes)){
                    appendBlobClient.AppendBlock(stream);
                }
                return true;
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to append to blob {blobName}");
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        // Reads a whole blob back into memory - used by LogController's read endpoints to proxy a
        // device's log/core dump content straight through rather than handing out a SAS URL, since
        // (unlike images) these are small text/binary files a browser or curl should just get directly.
        public byte[] DownloadBytes(string blobName){
            try{
                _logger.LogDebug($"DownloadBytes(\"{blobName}\")");
                Azure.Storage.Blobs.BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
                if(!blobClient.Exists()){
                    return null;
                }
                Azure.Storage.Blobs.Models.BlobDownloadResult result = blobClient.DownloadContent();
                return result.Content.ToArray();
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to download blob {blobName}");
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        // Lists blob names under `prefix` - used by LogController to list which days/core dumps
        // are available for a device without needing a DB table to track them (the blob container
        // is the authoritative list).
        public List<string> ListBlobNames(string prefix){
            try{
                _logger.LogDebug($"ListBlobNames(\"{prefix}\")");
                return blobContainerClient.GetBlobs(prefix: prefix).Select(b => b.Name).ToList();
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to list blobs with prefix {prefix}");
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