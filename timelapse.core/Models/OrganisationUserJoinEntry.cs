using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public class OrganisationUserJoinEntry
{
    public int Id { get; set; }
    public string UserId { get; set; }
    // No public User User because stupid
    public int OrganisationId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Organisation Organisation { get; set; }    
    public DateTime CreationDate { get; set; }
    public bool OrganisationAdmin { get; set; }
    public bool OrganisationOwner { get; set; }
}