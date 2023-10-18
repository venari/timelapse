using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
// using System.ComponentModel;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;

namespace timelapse.api.Helpers
{
    abstract class ContainerHelper{
        protected Container _container;
        protected readonly IMemoryCache _memoryCache;

        public ContainerHelper(Container container, IMemoryCache memoryCache){
            _container = container;
            _memoryCache = memoryCache;
        }

        public abstract Uri Upload(string blobName, Stream stream);
        public abstract string GenerateSasToken();

    }

    class ContainerHelper_Azure_Blob: ContainerHelper
    {
        private Azure.Storage.Blobs.BlobContainerClient blobContainerClient = null;

        public ContainerHelper_Azure_Blob(Container_Azure_Blob container, IMemoryCache memoryCache): base(container, memoryCache){
            blobContainerClient = new Azure.Storage.Blobs.BlobContainerClient(container.ConnectionString, container.Name);
        }

        public override Uri Upload(string blobName, Stream stream){
            Azure.Storage.Blobs.BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
            var blobContentInfo = blobClient.Upload(stream, true);
            return blobClient.Uri;
        }

        public override string GenerateSasToken(){
            var sasUri = blobContainerClient.GenerateSasUri(Azure.Storage.Sas.BlobContainerSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
            return sasUri.Query;
        }
    }

    class ContainerHelper_AWS_S3: ContainerHelper
    {
        private Amazon.S3.AmazonS3Client s3Client = null;

        public ContainerHelper_AWS_S3(Container_AWS_S3 container, IMemoryCache memoryCache): base(container, memoryCache){
        }

        public override Uri Upload(string blobName, Stream stream){
            throw new NotImplementedException();
        }

        public override string GenerateSasToken(){
            throw new NotImplementedException();
        }
    }

    class StorageHelper{

        private IConfiguration config;
        private ILogger _logger { get; }
        private AppDbContext _appDbContext;
        private readonly IMemoryCache _memoryCache;

        private Dictionary<string, ContainerHelper> ContainerHelpers = new Dictionary<string, ContainerHelper>();

        // public string SasUri {get; private set;}

        public StorageHelper(IConfiguration configuration, AppDbContext appDbContext, ILogger logger, IMemoryCache memoryCache){
            config = configuration;
            _appDbContext = appDbContext;
            _logger = logger;
            _memoryCache = memoryCache;

            Container_Azure_Blob defaultContainer = new Container_Azure_Blob();
            defaultContainer.Name = config["STORAGE_CONTAINER_NAME"];
            defaultContainer.ConnectionString = config["STORAGE_CONNECTION_STRING"];

            ContainerHelpers.Add("default", new ContainerHelper_Azure_Blob(defaultContainer, memoryCache));
        }

        // public bool Download(string blobName, string localFilePath){
        //     try{
        //         _logger.LogDebug($"Download(\"{blobName}\")");
        //         Azure.Storage.Blobs.Models.BlobDownloadInfo blobDownloadInfo = blobContainerClient.GetBlobClient(blobName).Download();
                
        //         using(FileStream fileStream = File.OpenWrite(localFilePath)){
        //             blobDownloadInfo.Content.CopyTo(fileStream);
        //         }

        //         return true;
        //     }
        //     catch(Exception ex){
        //         _logger.LogError($"Error trying to access blob {blobName}");
        //         _logger.LogError(ex.ToString());
        //         throw;
        //     }
        // }

        private string GetContainerHelperId(Container? containerOverride){    
            if(containerOverride==null){
                return "default";
            } else {
                return $"{containerOverride.Id}-{containerOverride.Name}";
            }
        }

        private ContainerHelper GetContainerHelper(Container? containerOverride){
            ContainerHelper containerHelper;

            // if(containerOverride==null){
            //     return ContainerHelpers["default"];
            // }

            // string containerHelpersKey = $"{containerOverride.Id}-{containerOverride.Name}";
            string containerHelpersKey = GetContainerHelperId(containerOverride);
            if(ContainerHelpers.ContainsKey(containerHelpersKey)){
                containerHelper = ContainerHelpers[containerHelpersKey];
                return containerHelper;
            }

            _logger.LogDebug("Container override not found, creating new ContainerHelper");

            if(containerOverride is Container_Azure_Blob){
                containerHelper = new ContainerHelper_Azure_Blob(containerOverride as Container_Azure_Blob, _memoryCache);
            } else if(containerOverride is Container_AWS_S3){
                containerHelper = new ContainerHelper_AWS_S3(containerOverride as Container_AWS_S3, _memoryCache);
            } else {
                throw new Exception("Unknown container type");
            }

            if(containerHelper==null){
                _logger.LogError("ContainerHelper is null for container override {containerName}", containerOverride.Name);
                throw new Exception("ContainerHelper is null");
            }

            ContainerHelpers.Add(containerHelpersKey, containerHelper);

            return containerHelper;
        }

        public Uri Upload(string blobName, Stream stream, Container? containerOverride){
            try{
                _logger.LogDebug($"Upload(\"{blobName}\")");

                ContainerHelper containerHelper = GetContainerHelper(containerOverride);

                Uri returnUri = containerHelper.Upload(blobName, stream);

                return returnUri;
            }
            catch(Exception ex){
                _logger.LogError("Error trying to access blob {blobName}", blobName);
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        private string GenerateSasToken(Container? containerOverride){
            try{

                string containerHelpersKey = GetContainerHelperId(containerOverride);
                string SasUriKey = containerHelpersKey + "SasUri";

                string token;
                if(!_memoryCache.TryGetValue(SasUriKey, out token)){
                    ContainerHelper containerHelper = GetContainerHelper(containerOverride);

                    token = containerHelper.GenerateSasToken();
                    _memoryCache.Set(SasUriKey, token, TimeSpan.FromHours(1));
                } 

                return token;
            }
            catch(Exception ex){
                _logger.LogError($"Error trying to GenerateSasUri");
                _logger.LogError(ex.ToString());
                return "";
            }
        }

        public string SasToken(int imageId){
            var containerOverride = GetContainerOverrideForImage(imageId);
            return GenerateSasToken(containerOverride);
        }

        public Container? GetContainerOverrideForImage(int imageId){
            var image = _appDbContext.Images
                .Include(i => i.Device)
                .FirstOrDefault(i => i.Id == imageId);

            if(image == null){
                return null;
            }

            var device = image.Device;

            if(device == null){
                return null;
            }

            Project? project = _appDbContext.Projects
                .Include(p => p.DeviceProjectContracts)
                .ThenInclude(dpc => dpc.Device)
                .Include(p => p.ContainerOveride)
                .Where(p => p.DeviceProjectContracts
                    .Any(dpc => dpc.DeviceId == device.Id && dpc.StartDate <= image.Timestamp && (dpc.EndDate == null || dpc.EndDate >= image.Timestamp)))
                .FirstOrDefault();

            Container? containerOveride=null;
            if(project!=null){
                containerOveride = project.ContainerOveride;
            }

            return containerOveride;
        }
        // public string SasToken{
        //     get{
        //         return GenerateSasUri().Query;
        //     }
        // }
    }
}