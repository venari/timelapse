using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace timelapse.core.models;

public class ProjectUser
{
    public int Id {get; set;}
 
    [Required]
    public IdentityUser User {get; set;}

    [Required]
    public Project Project {get; set;}

    [Required]
    public bool IsAdmin {get; set;}
}