using Microsoft.AspNetCore.Mvc;
using timelapse.api.Helpers;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    public class ImageController{

        public ImageController(AppDbContext appDbContext, ILogger<ImageController> logger, IConfiguration configuration){
            _appDbContext = appDbContext;
            _logger = logger;
            _storageHelper = new StorageHelper(configuration, logger);
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
                Timestamp = model.Timestamp==DateTime.MinValue?DateTime.Now.ToUniversalTime():model.Timestamp,
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