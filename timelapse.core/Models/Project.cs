using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace timelapse.core.models;

public class Project
{
    public int Id {get; set;}
    [Required]
    public DateTime StartDate {get; set;}
    [Required]
    public DateTime EndDate {get; set;}
    [Required]
    public string Name {get; set;}
    public string Description {get; set;}
    
    [System.Text.Json.Serialization.JsonIgnore]
    public List<DevicePlacement> DevicePlacements {get;} = new List<DevicePlacement>();

    [Required]
    public List<ProjectUser> ProjectUsers {get;} = new List<ProjectUser>();
}