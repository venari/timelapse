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
    [Authorize]
    public class ImageController{

        public ImageController(AppDbContext appDbContext, ILogger<ImageController> logger, IConfiguration configuration, IMemoryCache memoryCache){
            _appDbContext = appDbContext;
            _logger = logger;
            _storageHelper = new StorageHelper(configuration, logger, memoryCache);
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;
        private StorageHelper _storageHelper;
        
        // ESP32 devices upload here directly, identified by SerialNumber - no user login
        // involved, so this stays open regardless of the class-level [Authorize] above.
        [AllowAnonymous]
        [HttpPost]
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

        // Return latest image for device as a JPEG - gated by its own ThirdPartyApiKeyAuth
        // filter instead of the class-level [Authorize] (still lets a logged-in user
        // through too, since that filter checks User.Identity.IsAuthenticated first).
        [AllowAnonymous]
        [HttpGet("Latest")]
        [ThirdPartyApiKeyAuth]
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

        [HttpGet("GetImageAtOrAround")]
        // [ThirdPartyApiKeyAuth]
        public ActionResult<Image> GetImageAtOrAround([FromQuery] int deviceId, DateTime timestamp, bool forwards){
            // Fowards == true - get at or after timestamp
            // Forwards == false - get at or before timestamp
            
            Device device = _appDbContext.Devices.FirstOrDefault(d => d.Id == deviceId);

            if(device==null){
                return new NotFoundResult();
            }

            Image image = null;
            
            if(forwards){
                image = _appDbContext.Images
                    .Where(i => i.DeviceId == device.Id && i.Timestamp >= timestamp.ToUniversalTime())
                    .OrderBy(i => i.Timestamp)
                    .FirstOrDefault();
            } else {
                image = _appDbContext.Images
                    .Where(i => i.DeviceId == device.Id && i.Timestamp <= timestamp.ToUniversalTime())
                    .OrderBy(i => i.Timestamp)
                    .LastOrDefault();
            }
            if(image==null){
                return new NotFoundResult();
            }

            return image;

            // return new RedirectResult(image.BlobUri.ToString() + _storageHelper.SasToken);
        }

        [HttpGet("GetImagesBetweenDates")]
        public ActionResult<IEnumerable<Image>> GetImagesBetweenDates([FromQuery] int deviceId, DateTime startDate, DateTime endDate){
            Device device = _appDbContext.Devices.FirstOrDefault(d => d.Id == deviceId);

            if(device==null){
                return new NotFoundResult();
            }

            var images = _appDbContext.Images
                .Where(i => i.DeviceId == device.Id && i.Timestamp >= startDate.ToUniversalTime() && i.Timestamp <= endDate.ToUniversalTime())
                .OrderBy(i => i.Timestamp)
                .ToList();

            return images;
        }

        // OPTION 1: Proxy endpoint - serves the image through the API (RECOMMENDED)
        [HttpGet("Proxy/{imageId}")]
        public async Task<IActionResult> ProxyImage(int imageId){
            var image = _appDbContext.Images.FirstOrDefault(i => i.Id == imageId);
            
            if(image == null){
                return new NotFoundResult();
            }

            // Get the image from blob storage with SAS token
            var imageUrl = image.BlobUri + _storageHelper.SasToken;
            
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(imageUrl);
            
            if (!response.IsSuccessStatusCode){
                return new StatusCodeResult((int)response.StatusCode);
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
            
            return new FileContentResult(imageBytes, contentType);
        }

        // OPTION 2: Get SAS token endpoint - client appends token to blob URLs
        [HttpGet("SasToken")]
        public ActionResult<string> GetSasToken(){
            return _storageHelper.SasToken;
        }
    }
}