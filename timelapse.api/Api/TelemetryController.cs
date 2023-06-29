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

            // _logger.LogInformation("In Telemetry Post");
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

            Telemetry telemetry = new Telemetry(){
                DeviceId = device.Id,
                Timestamp = model.Timestamp.HasValue?model.Timestamp.Value:DateTime.Now.ToUniversalTime(),
                TemperatureC = model.TemperatureC,
                BatteryPercent = model.BatteryPercent,
                Status = model.Status,
                DiskSpaceFree = model.DiskSpaceFree,
                PendingImages = model.PendingImages,
                UploadedImages = model.UploadedImages,
                PendingTelemetry = model.PendingTelemetry,
                UploadedTelemetry = model.UploadedTelemetry,
                UptimeSeconds = model.UptimeSeconds
            };
            _appDbContext.Telemetry.Add(telemetry);
            _appDbContext.SaveChanges();
            return telemetry;
        }

        [HttpGet("GetLatest24HoursTelemetry")]
        public ActionResult<IEnumerable<Telemetry>> GetLatest24HoursTelemetry([FromQuery] int deviceId){
            _logger.LogInformation("Get latest 24 hours' telemetry");

            DateTime ?latestTelemetryDateTime = _appDbContext.Telemetry
                .Where(t => t.DeviceId == deviceId)
                .OrderByDescending(t => t.Timestamp)
                .Select(t => t.Timestamp)
                .FirstOrDefault();

            List<Telemetry> telemetry = new List<Telemetry>();

            if(latestTelemetryDateTime==null || !latestTelemetryDateTime.HasValue || latestTelemetryDateTime.Value == DateTime.MinValue){
                return new NotFoundObjectResult(telemetry);
            }

            Device? device = _appDbContext.Devices
                .Include(d => d.Telemetries.Where(t =>t.Timestamp >= latestTelemetryDateTime.Value.AddDays(-1)))
                .FirstOrDefault(d => d.Id == deviceId);

            if(device != null){
                telemetry =  device.Telemetries.OrderBy(t => t.Timestamp).ToList();
                // telemetry =  device.Telemetries.OrderBy(t => t.Timestamp).ToList();
            }

            return telemetry;
        }
 
        [HttpGet("GetLatestTelemetry")]
        public ActionResult<IEnumerable<Telemetry>> GetLatestTelemetry([FromQuery] int deviceId, int numberOfHoursToDisplay){
            _logger.LogInformation($"Get latest {numberOfHoursToDisplay} hours' telemetry");

            DateTime ?latestTelemetryDateTime = _appDbContext.Telemetry
                .Where(t => t.DeviceId == deviceId)
                .OrderByDescending(t => t.Timestamp)
                .Select(t => t.Timestamp)
                .FirstOrDefault();

            List<Telemetry> telemetry = new List<Telemetry>();

            if(latestTelemetryDateTime==null || !latestTelemetryDateTime.HasValue || latestTelemetryDateTime.Value == DateTime.MinValue){
                return new NotFoundObjectResult(telemetry);
            }

            Device? device = _appDbContext.Devices
                .Include(d => d.Telemetries.Where(t =>t.Timestamp >= latestTelemetryDateTime.Value.AddHours(-1 * numberOfHoursToDisplay)))
                .FirstOrDefault(d => d.Id == deviceId);

            if(device != null){
                telemetry =  device.Telemetries.OrderBy(t => t.Timestamp).ToList();
                // telemetry =  device.Telemetries.OrderBy(t => t.Timestamp).ToList();
            }

            return telemetry;
        }

        [HttpGet("GetTelemetryBetweenDates")]
        public ActionResult<IEnumerable<Telemetry>> GetTelemetryBetweenDates([FromQuery] int deviceId, DateTime startDate, DateTime endDate){
            _logger.LogInformation($"Get latest telemetry between {startDate} and {endDate}");

            List<Telemetry> telemetry = new List<Telemetry>();

            Device? device = _appDbContext.Devices
                .Include(d => d.Telemetries.Where(t =>t.Timestamp.ToUniversalTime() >= startDate.ToUniversalTime() && t.Timestamp.ToUniversalTime() <= endDate.ToUniversalTime()))
                .FirstOrDefault(d => d.Id == deviceId);

            if(device != null){
                telemetry =  device.Telemetries.OrderBy(t => t.Timestamp).ToList();
            }

            if(telemetry.Count==0){
                return new NotFoundObjectResult(telemetry);
            }

            return telemetry;
        }


    }
}