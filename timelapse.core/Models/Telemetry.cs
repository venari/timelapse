using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class Telemetry
{
    public int Id {get; set;}
    [Required]
    public string DeviceId {get; set;}
    [Required]
    public DateTime Timestamp {get; set;}
    [Required]
    public int TemperatureC {get; set;}
    [Required]
    public int BatteryPercent {get; set;}
    [Required]
    public int DiskSpaceFree {get; set;}
    [Required]
    public int UptimeSeconds {get; set;}
}
