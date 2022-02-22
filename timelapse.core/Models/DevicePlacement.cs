using System.ComponentModel.DataAnnotations;
using NetTopologySuite;

namespace timelapse.core.models;

public class DevicePlacement
{
    public int Id {get; set;}
    [Required]
    public DateTime StartDate {get; set;}
    [Required]
    public DateTime EndDate {get; set;}

    [Required]
    public int DeviceId {get; set;}
    public Device Device {get; set;}

    [Required]
    public int ProjectId {get; set;}
    public Project Project {get; set;}

    [Required]
    public NetTopologySuite.Geometries.Point Location {get; set;}
    [Required]
    public int Direction {get; set;}

    public string Description {get; set;}

}