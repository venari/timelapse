using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int OrganisationID { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Organisation Organisation { get; set; }
    public List<DeviceProjectContract> DeviceProjectContracts { get; } = new List<DeviceProjectContract>();
}