using Microsoft.AspNetCore.Mvc;
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

        [HttpPost]
        public ActionResult<Image> Post([FromForm] ImagePostModel model){

            Image image = new Image(){
                DeviceId = model.DeviceId,
                Timestamp = model.Timestamp.HasValue?model.Timestamp.Value:DateTime.Now.ToUniversalTime(),
                // file = model.file
            };

            string blobName = model.DeviceId + "_" + model.File.FileName;
            image.BlobUri = _storageHelper.Upload(blobName, model.File.OpenReadStream());

            _logger.LogInformation("Add Image");
            _appDbContext.Images.Add(image);
            _appDbContext.SaveChanges();
            return image;
        }
    }
}