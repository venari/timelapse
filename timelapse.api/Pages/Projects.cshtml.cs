using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace timelapse.api.Pages;

public class ProjectsModel : PageModel
{
    private readonly ILogger<ProjectsModel> _logger;
    private AppDbContext _appDbContext;

    public List<Project> projects {get;}

    public ProjectsModel(ILogger<ProjectsModel> logger, AppDbContext appDbContext, IConfiguration configuration)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        projects = _appDbContext.Projects
            .Include(p => p.DevicePlacements)
            .ThenInclude(dp => dp.Device)
            .Include(p => p.ProjectUsers)
            .ThenInclude(pu => pu.User)
            .AsSplitQuery()
            .ToList();
    }

    public void OnGet()
    {
        // return 
    }

}