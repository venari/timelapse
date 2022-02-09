using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            return _appDbContext.Devices.Include(d => d.Telemetries).ToList();
        }

        [HttpGet("UnregisteredDevices")]
        public ActionResult<IEnumerable<UnregisteredDevice>> GetUnregisteredDecices(){
            _logger.LogInformation("Get unregistered devices");
            return _appDbContext.UnregisteredDevices.ToList();
        }

        [HttpPost]
        public ActionResult<Device> Post([FromForm] DevicePostModel model){
            _logger.LogInformation("Add device");
            
            Device device = new Device(){
                Name = model.Name,
                SerialNumber = model.SerialNumber,
                Description = model.Description
            };
            
            _appDbContext.Devices.Add(device);
            _appDbContext.SaveChanges();
            return device;
        }
    }
}