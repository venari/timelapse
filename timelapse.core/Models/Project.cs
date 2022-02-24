using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace timelapse.core.models;

public class Project
{
    public int Id {get; set;}
    public DateTime? StartDate {get; set;}
    public DateTime? EndDate {get; set;}
    [Required]
    public string Name {get; set;}
    public string Description {get; set;}
    public bool Archived {get; set;}
    
    [System.Text.Json.Serialization.JsonIgnore]
    public List<DevicePlacement> DevicePlacements {get;} = new List<DevicePlacement>();

    [Required]
    public List<ProjectUser> ProjectUsers {get;} = new List<ProjectUser>();
}