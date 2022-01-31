using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class Device
{
    public int Id {get; set;}
    [Required]
    public string Name {get; set;}
    public string Description {get; set;}

    [System.Text.Json.Serialization.JsonIgnore]
    public List<Telemetry> Telemetries {get;} = new List<Telemetry>();

    [System.Text.Json.Serialization.JsonIgnore]
    public List<Image> Images {get;} = new List<Image>();

    public Telemetry? LatestTelemetry {
        get{
            var latestTelemetry = Telemetries.OrderByDescending(t => t.Timestamp).FirstOrDefault();
            return latestTelemetry;
        }
    }

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

    public Image? LatestImage {
        get{
            var latestImage = Images.OrderByDescending(i => i.Timestamp).FirstOrDefault();
            return latestImage;
        }
    }
}

