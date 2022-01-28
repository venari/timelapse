using Microsoft.AspNetCore.Mvc;
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
                DiskSpaceFree = model.DiskSpaceFree,
                UptimeSeconds = model.UptimeSeconds
            };
            _logger.LogInformation("Add Telemetry BLAH");
            _appDbContext.Telemetry.Add(telemetry);
            _appDbContext.SaveChanges();
            return telemetry;
        }
    }
}