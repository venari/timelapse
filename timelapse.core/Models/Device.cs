using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using timelapse.core.Helpers;

namespace timelapse.core.models;

public class Device
{
    public int Id {get; set;}
    [Required]
    public string SerialNumber {get; set;}
    [Required]
    public string Name {get; set;}
    public string ShortDescription {get; set;}
    public string Description {get; set;}

    public bool SupportMode {get; set;} = false;
    public bool MonitoringMode {get; set;} = false;
    public bool Retired {get; set;} = false;
    public bool HibernateMode {get; set;} = false;
    public bool PowerOff {get; set;} = false;
    public bool Service {get; set;} = false;
    public bool WideAngle {get; set;} = false;

    [System.Text.Json.Serialization.JsonIgnore]
    public List<Telemetry> Telemetries {get;} = new List<Telemetry>();

    [System.Text.Json.Serialization.JsonIgnore]
    public List<Image> Images {get;} = new List<Image>();

    [System.Text.Json.Serialization.JsonIgnore]
    public List<Event> Events {get;} = new List<Event>();

    private Telemetry? latestTelemetry = null;
    [System.Text.Json.Serialization.JsonIgnore]
    public Telemetry? LatestTelemetry {
        get{
            if(latestTelemetry == null){
                latestTelemetry = Telemetries.OrderByDescending(t => t.Timestamp).FirstOrDefault();

                // ESP32S3 voltage to percentage hack
                if(latestTelemetry!=null && latestTelemetry.BatteryPercent == 0 && latestTelemetry.BatteryVoltage > 0){
                    latestTelemetry.BatteryPercent = VoltageToPercentageHelper.VoltageToPercentage(latestTelemetry.BatteryVoltage.Value/1000.0);
                }
            }
            return latestTelemetry;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime? LatestTelemetryTimestamp {
        get{
            if(latestTelemetry == null){
                latestTelemetry = Telemetries.OrderByDescending(t => t.Timestamp).FirstOrDefault();
            }
            if(latestTelemetry!=null){
                return latestTelemetry.Timestamp;
            } else {
                return null;
            }
        }
    }

    // [System.Text.Json.Serialization.JsonIgnore]
    // public string HumanizedLatestTelemetryAge {
    //     get{
    //         var latestTelemetry = Telemetries.OrderByDescending(t => t.Timestamp).FirstOrDefault();
    //         if(latestTelemetry!=null){
    //             return $"{(DateTime.UtcNow - latestTelemetry.Timestamp).Humanize()} ago";
    //         }
    //         return "Missing";
    //     }
    // }

    // public string Last24HoursBatteryAsText{
    //     get{
    //         var last24HoursBatteryAsText = "";
    //         // Telemetries.Where(t =>t.Timestamp.Date >= DateTime.UtcNow.AddDays(-1).Date).ToList().ForEach(t => last24HoursBatteryAsText += $"{t.BatteryPercent},");
    //         // var latest24HoursAsArray = Telemetries.Where(t =>t.Timestamp.Date >= DateTime.UtcNow.AddDays(-1).Date).Select(t => new {x = new DateTime(t.Timestamp.ToUniversalTime().Ticks), y = t.BatteryPercent});
    //         // var latest24HoursAsArray = Telemetries.Where(t =>t.Timestamp.Date >= DateTime.UtcNow.AddDays(-1).Date).Select(t => new {x = t.Timestamp, y = t.BatteryPercent});

    //         // last24HoursBatteryAsText = System.Text.Json.JsonSerializer.Serialize(latest24HoursAsArray, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            
    //         last24HoursBatteryAsText = "[";
    //         foreach(var telemetry in Telemetries.Where(t =>t.Timestamp.Date >= DateTime.UtcNow.AddDays(-1).Date)){
    //             last24HoursBatteryAsText += $"{{ x: '{telemetry.Timestamp.ToUniversalTime().ToString("o")}', y: {telemetry.BatteryPercent} }},";
    //         }
    //         last24HoursBatteryAsText = last24HoursBatteryAsText.TrimEnd(',');
    //         last24HoursBatteryAsText += "]";

    //         return last24HoursBatteryAsText;
    //         }
    // }

    [System.Text.Json.Serialization.JsonIgnore]
    public Image? LatestImage {
        get{
            var latestImage = Images.OrderByDescending(i => i.Timestamp).FirstOrDefault();
            return latestImage;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime? LatestImageTimestamp {
        get{
            var latestImage = Images.OrderByDescending(i => i.Timestamp).FirstOrDefault();
            if(latestImage!=null){
                return latestImage.Timestamp;
            } else {
                return null;
            }
        }
    }

    // [System.Text.Json.Serialization.JsonIgnore]
    // public string HumanizedLatestImageAge {
    //     get{
    //         var latestImage = Images.OrderByDescending(i => i.Timestamp).FirstOrDefault();
    //         if(latestImage!=null){
    //             return $"{(DateTime.UtcNow - latestImage.Timestamp).Humanize()} ago";
    //         }
    //         return "Missing";
    //     }
    // }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public List<DeviceProjectContract> DeviceProjectContracts { get; } = new List<DeviceProjectContract>();

    [System.Text.Json.Serialization.JsonIgnore]
    public List<DeviceLocation> DeviceLocations { get; } = new List<DeviceLocation>();

    [NotMapped]
    [System.Text.Json.Serialization.JsonIgnore]
    public DeviceLocation? CurrentLocation {
        get {
            var currentLocation = DeviceLocations.OrderByDescending(l => l.Timestamp).FirstOrDefault();
            return currentLocation;
        }

        // set {
        //     DeviceLocations.Add(value);
        // }
    }
}

