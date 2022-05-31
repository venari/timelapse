using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using timelapse.api.Helpers;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    public class ImageController{

        public ImageController(AppDbContext appDbContext, ILogger<ImageController> logger, IConfiguration configuration, IMemoryCache memoryCache){
            _appDbContext = appDbContext;
            _logger = logger;
            _storageHelper = new StorageHelper(configuration, logger, memoryCache);
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;
        private StorageHelper _storageHelper;

        [HttpGet]
        public ActionResult<IEnumerable<Image>> Get(){
            _logger.LogInformation("Get all TelemetryController");
            return _appDbContext.Images.ToList();
        }

        [HttpGet("GetImage")]
        public ActionResult<Image> GetImage([FromQuery] int deviceId, int imageIndex){
            _logger.LogInformation("Get Image");
            // var device = _appDbContext.Devices.Find(deviceId);
            var device = _appDbContext.Devices
                .Include(d => d.Images)
                .FirstOrDefault(d => d.Id == deviceId);

            if(device==null){
                return new NotFoundResult();
            }
            var images = device.Images.OrderBy(i => i.Timestamp).ToList();
            if(imageIndex<0 || imageIndex>=images.Count){
                return new NotFoundResult();
            }
            var image = images[imageIndex];
            return image;
            // var imageUrl = image.BlobUri + _storageHelper.SasToken;
            // return sasUri.ToString();
        }
        
        [HttpPost]
        public ActionResult<Image> Post([FromForm] ImagePostModel model){

            _logger.LogInformation("In Image Post");
            // _logger.LogInformation("DeviceId: " + model.DeviceId);
            _logger.LogInformation("SerialNumber: " + model.SerialNumber);
            _logger.LogInformation("Timestamp: " + model.Timestamp);


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

            _logger.LogInformation("Add Image");
            _appDbContext.Images.Add(image);
            _appDbContext.SaveChanges();
            return image;
        }
    }
}