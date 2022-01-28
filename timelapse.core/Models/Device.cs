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

    public Image? LatestImage {
        get{
            var latestImage = Images.OrderByDescending(i => i.Timestamp).FirstOrDefault();
            return latestImage;
        }
    }
}

