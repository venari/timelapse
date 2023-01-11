using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class DeviceProjectContract
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int ProjectId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Project Project { get; set; }
    public int DeviceId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Device Device { get; set; }
}