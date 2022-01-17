using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class Device
{
    public int Id {get; set;}
    [Required]
    public string Name {get; set;}
    public string Description {get; set;}
}
