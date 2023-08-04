using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class Event
{
    public int Id {get; set;}
    [Required]
    public DateTime StartTime {get; set;}
    [Required]
    public DateTime EndTime {get; set;}

    [Required]
    public DateTime CreatedDate {get; set;} = DateTime.UtcNow;
    [Required]
    public string CreatedByUserId {get; set;}

    public string Description {get; set;}

    [Required]
    public int DeviceId {get; set;}
    public Device Device {get; set;}

    public List<EventComment> Comments {get; set;} = new List<EventComment>();
}