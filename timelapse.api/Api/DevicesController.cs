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
        public ActionResult<IEnumerable<object>> GetDevices(){
            _logger.LogInformation("Get all devices");
            var devices = _appDbContext.Devices
                .Where(d => !d.Retired)
                .ToList();
            var deviceIds = devices.Select(d => d.Id).ToList();

            // Only look at recent telemetry (last 7 days) to avoid loading too much data
            var recentCutoff = DateTime.UtcNow.AddDays(-7);

            // Get latest telemetry per device (only from last 7 days)
            var latestTelemetries = _appDbContext.Telemetry
                .Where(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= recentCutoff)
                .ToList()
                .GroupBy(t => t.DeviceId)
                .Select(g => g.OrderByDescending(t => t.Timestamp).First())
                .ToDictionary(t => t.DeviceId);

            // Get latest image per device (only from last 7 days)
            var latestImages = _appDbContext.Images
                .Where(i => deviceIds.Contains(i.DeviceId) && i.Timestamp >= recentCutoff)
                .ToList()
                .GroupBy(i => i.DeviceId)
                .Select(g => g.OrderByDescending(i => i.Timestamp).First())
                .ToDictionary(i => i.DeviceId);

            // Build result
            var result = devices.Select(d => new {
                d.Id,
                d.SerialNumber,
                d.Name,
                d.ShortDescription,
                d.Description,
                d.SupportMode,
                d.MonitoringMode,
                d.Retired,
                d.HibernateMode,
                d.PowerOff,
                d.Service,
                d.WideAngle,
                LatestTelemetry = latestTelemetries.ContainsKey(d.Id) ? latestTelemetries[d.Id] : null,
                LatestImage = latestImages.ContainsKey(d.Id) ? latestImages[d.Id] : null,
                DeviceLocations = new List<DeviceLocation>()
            }).ToList();

            return result;
        }

        [HttpGet("{id}")]
        public ActionResult<object> GetDevice(int id){
            _logger.LogInformation($"Get device {id}");
            var device = _appDbContext.Devices.FirstOrDefault(d => d.Id == id);
            
            if (device == null){
                return new NotFoundResult();
            }

            // Get latest telemetry for this device
            var latestTelemetry = _appDbContext.Telemetry
                .Where(t => t.DeviceId == id)
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefault();

            // Get latest image for this device
            var latestImage = _appDbContext.Images
                .Where(i => i.DeviceId == id)
                .OrderByDescending(i => i.Timestamp)
                .FirstOrDefault();

            // Get device locations
            var deviceLocations = _appDbContext.Set<DeviceLocation>()
                .Where(dl => dl.DeviceId == id)
                .ToList();
            
            var result = new {
                device.Id,
                device.SerialNumber,
                device.Name,
                device.ShortDescription,
                device.Description,
                device.SupportMode,
                device.MonitoringMode,
                device.Retired,
                device.HibernateMode,
                device.PowerOff,
                device.Service,
                device.WideAngle,
                LatestTelemetry = latestTelemetry,
                LatestImage = latestImage,
                DeviceLocations = deviceLocations
            };

            return result;
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
                ShortDescription = model.ShortDescription,
                Description = model.Description
            };
            
            _appDbContext.Devices.Add(device);
            _appDbContext.SaveChanges();
            return device;
        }
    }
}
