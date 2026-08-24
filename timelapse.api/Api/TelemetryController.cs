using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using timelapse.core.Helpers;
using timelapse.core.models;
using timelapse.infrastructure;

namespace timelapse.api{

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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

        // ESP32 devices upload here directly, identified by SerialNumber - no user login
        // involved, so this stays open regardless of the class-level [Authorize] above.
        [AllowAnonymous]
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
            _logger.LogInformation("Get latest 24 hours' telemetry");;

            return GetTelemetryBetweenDates(deviceId, new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0).AddHours(-24), DateTime.UtcNow);
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

            var next = _appDbContext.Telemetry
                .Where(t => t.DeviceId == deviceId && t.Timestamp.ToUniversalTime() > endDate.ToUniversalTime())
                .OrderBy(t => t.Timestamp)
                .FirstOrDefault();

            if(next!=null){
                telemetry.Add(next);
            }

            // ESP32S3 voltage to percentage hack
            foreach(var t in telemetry.Where(t => t.BatteryPercent == 0 && t.BatteryVoltage > 0)){
                t.BatteryPercent = VoltageToPercentageHelper.VoltageToPercentage(t.BatteryVoltage.Value/1000.0);
            }

            if(telemetry.Count==0){
                return new NotFoundObjectResult(telemetry);
            }

            // Determine if we should aggregate based on the time span and data volume
            var timeSpan = endDate - startDate;
            var bucketSize = DetermineBucketSize(timeSpan, telemetry.Count);

            // If bucket size is null, return raw data
            if (bucketSize == null)
            {
                return telemetry;
            }

            // Aggregate the data and convert to Telemetry format
            var aggregatedData = AggregateDataToTelemetry(telemetry, startDate, endDate, bucketSize.Value, deviceId);
            return aggregatedData;
        }

        private TimeSpan? DetermineBucketSize(TimeSpan timeSpan, int dataPointCount)
        {
            const int targetDataPoints = 200; // Target number of data points to return
            
            // If we have few data points, no need to aggregate
            if (dataPointCount <= targetDataPoints)
            {
                return null; // Return raw data
            }

            // Calculate the ideal bucket size based on data density
            var totalMinutes = timeSpan.TotalMinutes;
            var idealBucketMinutes = totalMinutes / targetDataPoints;

            // Round to sensible intervals
            if (idealBucketMinutes < 5)
                return TimeSpan.FromMinutes(5);
            else if (idealBucketMinutes < 15)
                return TimeSpan.FromMinutes(15);
            else if (idealBucketMinutes < 30)
                return TimeSpan.FromMinutes(30);
            else if (idealBucketMinutes < 60)
                return TimeSpan.FromHours(1);
            else if (idealBucketMinutes < 180)
                return TimeSpan.FromHours(3);
            else if (idealBucketMinutes < 360)
                return TimeSpan.FromHours(6);
            else if (idealBucketMinutes < 720)
                return TimeSpan.FromHours(12);
            else
                return TimeSpan.FromDays(1);
        }

        private List<Telemetry> AggregateDataToTelemetry(List<Telemetry> telemetry, DateTime startDate, DateTime endDate, TimeSpan bucketSize, int deviceId)
        {
            var aggregatedList = new List<Telemetry>();

            for (var bucketStart = startDate; bucketStart < endDate; bucketStart = bucketStart.Add(bucketSize))
            {
                var bucketEnd = bucketStart.Add(bucketSize);
                if (bucketEnd > endDate) bucketEnd = endDate;

                var bucketData = telemetry.Where(t => t.Timestamp >= bucketStart && t.Timestamp < bucketEnd).ToList();
                
                if (bucketData.Count == 0)
                    continue;

                // Create a Telemetry object using average values
                var aggregated = new Telemetry
                {
                    // Use bucket start as timestamp
                    Timestamp = bucketStart,
                    DeviceId = deviceId,
                    
                    // Use average values for numeric fields
                    TemperatureC = (int)Math.Round(bucketData.Average(t => t.TemperatureC)),
                    BatteryPercent = (int)Math.Round(bucketData.Average(t => t.BatteryPercent)),
                    
                    // Average for optional fields if present
                    DiskSpaceFree = bucketData.Where(t => t.DiskSpaceFree.HasValue).Any() 
                        ? (int?)Math.Round(bucketData.Where(t => t.DiskSpaceFree.HasValue).Average(t => t.DiskSpaceFree.Value))
                        : null,
                    
                    UptimeSeconds = bucketData.Where(t => t.UptimeSeconds.HasValue).Any() 
                        ? (int?)Math.Round(bucketData.Where(t => t.UptimeSeconds.HasValue).Average(t => t.UptimeSeconds.Value))
                        : null,
                    
                    PendingImages = bucketData.Where(t => t.PendingImages.HasValue).Any() 
                        ? (int?)Math.Round(bucketData.Where(t => t.PendingImages.HasValue).Average(t => t.PendingImages.Value))
                        : null,
                    
                    UploadedImages = bucketData.Where(t => t.UploadedImages.HasValue).Any() 
                        ? (int?)Math.Round(bucketData.Where(t => t.UploadedImages.HasValue).Average(t => t.UploadedImages.Value))
                        : null,
                    
                    PendingTelemetry = bucketData.Where(t => t.PendingTelemetry.HasValue).Any() 
                        ? (int?)Math.Round(bucketData.Where(t => t.PendingTelemetry.HasValue).Average(t => t.PendingTelemetry.Value))
                        : null,
                    
                    UploadedTelemetry = bucketData.Where(t => t.UploadedTelemetry.HasValue).Any() 
                        ? (int?)Math.Round(bucketData.Where(t => t.UploadedTelemetry.HasValue).Average(t => t.UploadedTelemetry.Value))
                        : null,
                    
                    // Use representative status from middle of bucket
                    Status = bucketData.OrderBy(t => t.Timestamp).Skip(bucketData.Count / 2).FirstOrDefault()?.Status
                };

                aggregatedList.Add(aggregated);
            }

            // Always include the latest telemetry point (raw data) to ensure most recent reading is visible
            var latestPoint = telemetry
                .Where(t => t.Timestamp >= startDate && t.Timestamp <= endDate)
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefault();
            
            if (latestPoint != null)
            {
                // Check if the latest point is already very close to our last aggregated point
                var lastAggregated = aggregatedList.LastOrDefault();
                if (lastAggregated == null || (latestPoint.Timestamp - lastAggregated.Timestamp).TotalSeconds > 60)
                {
                    // Add the latest point only if it's more than 1 minute after the last aggregated point
                    aggregatedList.Add(latestPoint);
                }
            }

            return aggregatedList;
        }


    }
}