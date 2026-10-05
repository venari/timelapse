using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class DeviceLocation
{
    public int Id {get; set;}
    [Required]
    public int DeviceId {get; set;}
    [Required]
    public Device Device {get; set;}
    [Required]
    public double Latitude {get; set;}
    [Required]
    public double Longitude {get; set;}
    [Required]
    public DateTime Timestamp {get; set;}
    public int? Heading {get; set;}
    public int? Pitch {get; set;}
    public int? HeightMM {get; set;}

    public string Description {get; set;}
}

