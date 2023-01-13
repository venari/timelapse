using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class Organisation
{
    public int Id { get; set; }
    public string Name { get; set; } = String.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Project> Projects { get; } = new List<Project>();
    [System.Text.Json.Serialization.JsonIgnore]
    public List<OrganisationUserJoinEntry> OrganisationUserJoinEntries { get; } = new List<OrganisationUserJoinEntry>();
}