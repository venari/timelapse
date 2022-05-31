using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class UnregisteredDevice
{
    public int Id {get; set;}
    [Required]
    public string SerialNumber {get; set;}
}