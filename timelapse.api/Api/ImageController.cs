using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using timelapse.api.Filters;
using timelapse.api.Helpers;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    [ThirdPartyApiKeyAuth]
    [AllowAnonymous]
    public class ImageController{

        public ImageController(AppDbContext appDbContext, ILogger<ImageController> logger, IConfiguration configuration, IMemoryCache memoryCache){
            _appDbContext = appDbContext;
            _logger = logger;
            _storageHelper = new StorageHelper(configuration, logger, memoryCache);
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;
        private StorageHelper _storageHelper;
        
        [HttpPost]
        [AllowAnonymous]
        public ActionResult<Image> Post([FromForm] ImagePostModel model){

            // _logger.LogInformation("In Image Post");
            // _logger.LogInformation("SerialNumber: " + model.SerialNumber);
            // _logger.LogInformation("Timestamp: " + model.Timestamp);


            Device device = _appDbContext.Devices.FirstOrDefault(d => d.SerialNumber == model.SerialNumber);

           if(device==null){
                UnregisteredDevice unregistered = _appDbContext.UnregisteredDevices.FirstOrDefault(d => d.SerialNumber == model.SerialNumber);

                if(unregistered==null){
                    unregistered = new UnregisteredDevice(){
                        SerialNumber = model.SerialNumber
                    };

                    _appDbContext.UnregisteredDevices.Add(unregistered);
                    _appDbContext.SaveChanges();
                }

                return new NotFoundResult();
            }

            Image image = new Image(){
                DeviceId = device.Id,
                Timestamp = model.Timestamp.HasValue?model.Timestamp.Value:DateTime.Now.ToUniversalTime(),
                // file = model.file
            };

            string blobName = device.Id + "_" + model.File.FileName;
            image.BlobUri = _storageHelper.Upload(blobName, model.File.OpenReadStream());

            // _logger.LogInformation("Add Image");
            _appDbContext.Images.Add(image);
            _appDbContext.SaveChanges();
            return image;
        }

        // Return latest image for device as a JPEG
        [HttpGet("Latest")]
        public ActionResult GetLatest([FromQuery] int deviceId){
            Device device = _appDbContext.Devices.FirstOrDefault(d => d.Id == deviceId);

            if(device==null){
                return new NotFoundResult();
            }

            Image image = _appDbContext.Images
                .Where(i => i.DeviceId == device.Id)
                .OrderByDescending(i => i.Timestamp)
                .FirstOrDefault();

            if(image==null){
                return new NotFoundResult();
            }

            return new RedirectResult(image.BlobUri.ToString() + _storageHelper.SasToken);
        }        
    }
}