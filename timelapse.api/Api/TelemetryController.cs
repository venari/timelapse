using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    public class TelemetryController{

        public TelemetryController(AppDbContext appDbContext, ILogger<TelemetryController> logger){
            _appDbContext = appDbContext;
            _logger = logger;
        }

        private AppDbContext _appDbContext;
        private ILogger _logger;

        [HttpGet]
        public ActionResult<IEnumerable<Telemetry>> Get(){
            _logger.LogInformation("Get all TelemetryController");
            return _appDbContext.Telemetry.ToList();
        }

        [HttpPost]
        public ActionResult<Telemetry> Post([FromForm] TelemetryPostModel model){

            _logger.LogInformation("In Telemenrty Post");
            _logger.LogInformation("DeviceId: " + model.DeviceId);
            _logger.LogInformation("Timestamp: " + model.Timestamp);

            Telemetry telemetry = new Telemetry(){
                DeviceId = model.DeviceId,
                Timestamp = model.Timestamp.HasValue?model.Timestamp.Value:DateTime.Now.ToUniversalTime(),
                TemperatureC = model.TemperatureC,
                BatteryPercent = model.BatteryPercent,
                Status = model.Status,
                DiskSpaceFree = model.DiskSpaceFree,
                UptimeSeconds = model.UptimeSeconds
            };
            _logger.LogInformation("Add Telemetry BLAH");
            _appDbContext.Telemetry.Add(telemetry);
            _appDbContext.SaveChanges();
            return telemetry;
        }

        [HttpGet("GetLatest24HoursTelemetry")]
        public ActionResult<IEnumerable<Telemetry>> GetLatest24HoursTelemetry([FromQuery] int deviceId){
            _logger.LogInformation("Get latest 24 hours' telemetry");
            Device? device = _appDbContext.Devices
                .Include(d => d.Telemetries)
                .FirstOrDefault(d => d.Id == deviceId);

            List<Telemetry> telemetry = new List<Telemetry>();
            if(device != null){
                telemetry =  device.Telemetries.Where(t =>t.Timestamp.Date >= DateTime.UtcNow.AddDays(-1).Date).OrderBy(t => t.Timestamp).ToList();
            }

            return telemetry;
        }
    }
}