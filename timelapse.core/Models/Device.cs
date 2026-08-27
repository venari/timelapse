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

    // ESP32 camera config, pushed to the device over the Device object nested in every
    // Image/Telemetry POST response (see ImageController/TelemetryController) - the same
    // mechanism SupportMode etc. above already use. The ESP32 caches these on its SD card
    // and re-applies them each boot, so a change here takes effect next time it phones home.
    public bool SleepDuringNight {get; set;} = false;
    public int DaytimeStartsAtH {get; set;} = 7;
    public int DaytimeEndsAtH {get; set;} = 17;

    // Fixed offset from UTC, in minutes, the ESP32 uses to interpret DaytimeStartsAtH/
    // DaytimeEndsAtH as local wall-clock time (they're a working-day schedule, not sunlight -
    // camera exposure switching is worked out on-device from sunrise/sunset instead, see
    // isNightForExposure() in the .ino). Not a real timezone/DST lookup - just a plain offset -
    // so daylight saving currently means updating this by hand twice a year (NZST +720 / NZDT
    // +780). Defaults to NZST since that's every device deployed so far.
    public int UtcOffsetMinutes {get; set;} = 720;

    public int CameraIntervalS {get; set;} = 300;
    public bool Hflip {get; set;} = false;
    public bool Vflip {get; set;} = false;

    // Whether the ESP32 switches the camera to a slower-clock, fixed-exposure setup at night
    // (isNightForExposure() in the .ino, based on real sunrise/sunset - see UtcOffsetMinutes'
    // comment above for why that's a separate thing from DaytimeStartsAtH/DaytimeEndsAtH).
    // LongExposureXclkHz is the pixel clock (Hz) used while it's active - lower gives a longer
    // max exposure but hasn't been characterised against every OV5640 unit's PLL tolerance yet
    // (see setupCameraNightExposure()'s comment in the .ino).
    public bool EnableLongExposureAtNight {get; set;} = true;
    public int LongExposureXclkHz {get; set;} = 8000000;

    // How often (in seconds) the device checks GPS position (see updateGeoLocationIfDue() in
    // the .ino) and how often it reconnects to WiFi to sync its clock and upload its backlog
    // (see the needsSync check in setup()) - both pushed down the same way as CameraIntervalS
    // above.
    public int GeoIntervalS {get; set;} = 3600;
    public int AutoSyncPeriodS {get; set;} = 300;

    // Deliberately blank, not defaulted to a real URL: the ESP32 only overwrites its local
    // apiUrl when this is non-empty (see applyConfigFields in the .ino). A real default here
    // would mean every device - including ones seeded via SD card to point at a different
    // environment - gets redirected back to whatever URL this says the first time it uploads,
    // since a freshly-created Device row would otherwise already have an opinion. Leaving it
    // blank means the API stays silent on this field until someone deliberately sets it here.
    public string ApiUrl {get; set;} = "";

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

