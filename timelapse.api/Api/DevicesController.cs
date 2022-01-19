using Microsoft.AspNetCore.Mvc;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{
    [Route("api/[controller]")]
    [ApiController]
    public class DevicesController{

        public DevicesController(AppDbContext appDbContext, ILogger<DevicesController> logger){
            _appDbContext = appDbContext;
            _logger = logger;
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;

        [HttpGet]
        public ActionResult<IEnumerable<Device>> Get(){
            _logger.LogInformation("Get all devices");
            return _appDbContext.Devices.ToList();
        }

        [HttpPost]
        public ActionResult<Device> Post([FromQuery] Device device){
            _logger.LogInformation("Add device");
            _appDbContext.Devices.Add(device);
            _appDbContext.SaveChanges();
            return device;
        }
    }
}