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
        public ActionResult<Telemetry> Post([FromQuery] TelemetryPostModel model){

            Telemetry telemetry = new Telemetry(){
                DeviceId = model.DeviceId,
                Timestamp = model.Timestamp==DateTime.MinValue?DateTime.Now.ToUniversalTime():model.Timestamp,
                TemperatureC = model.TemperatureC,
                BatteryPercent = model.BatteryPercent,
                DiskSpaceFree = model.DiskSpaceFree,
                UptimeSeconds = model.UptimeSeconds
            };
            _logger.LogInformation("Add Telemetry");
            _appDbContext.Telemetry.Add(telemetry);
            _appDbContext.SaveChanges();
            return telemetry;
        }
    }
}