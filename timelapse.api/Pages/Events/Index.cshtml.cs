using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;

namespace timelapse.api.Pages.Events;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly UserManager<AppUser> _userManager;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public Project Project { get; private set; }
    public List<Device> devices {get; private set;}
    // public IEnumerable<Image> images {get; set;}
    // public string SasToken {get; private set;}
    public List<Areas.Identity.Data.AppUser> Users {get; private set;}

    public IndexModel(ILogger<IndexModel> logger, UserManager<AppUser> UserManager, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _userManager = UserManager;
        _appDbContext = appDbContext;

        // images = _appDbContext.Images;
        _storageHelper = new StorageHelper(configuration, appDbContext, logger, memoryCache);

        devices = new List<Device>();

    }

    // public string GetSasTokenForImage(int imageId){
    //     return _storageHelper.SasToken(imageId);
    // }

    private bool LoadProject(int projectId){
        Project = _appDbContext.Projects
            .Include(p => p.Organisation)
            .ThenInclude(o => o.Containers)
            .FirstOrDefault(p => p.Id == projectId);

        if(Project == null){
            return false;
        }

        var userId = _userManager.GetUserId(User);
        if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == Project.OrganisationId && e.UserId == userId))
        {
            _logger.LogWarning($"Unauthorised access attempt on project {Project.Id} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
            return false;
        }

        return true;
    }

    public ActionResult OnGet(int projectId)
    {
        if(!LoadProject(projectId)){
            return NotFound($"No project with ID {projectId}");
        }

        if(Project==null){
            return new NotFoundObjectResult("Project not found");
        }

        // SasToken = _storageHelper.SasToken(Project);

        // I think we'll need to do this in mulitple passes,
        // as I can't work out how to do it in one go in LINQ. 

        var contracts = _appDbContext.DeviceProjectContracts
            .Include(dpc => dpc.Device)
            .Where(dpc => dpc.ProjectId == projectId)
            .ToList();

        var projectEvents = new List<Event>();

        foreach(var contract in contracts){
            var deviceEvents = _appDbContext.Events
                .Include(e => e.Device)
                .Include(e => e.EventTypes)
                .Include(e => e.StartImage)
                .Include(e => e.EndImage)
                .Where(e => e.DeviceId == contract.DeviceId)
                .Where(e => e.StartTime >= contract.StartDate && (e.EndTime <= contract.EndDate || contract.EndDate == null))
                .ToList();

            projectEvents.AddRange(deviceEvents);

            if(!devices.Any(d => d.Id == contract.DeviceId)){
                devices.Add(contract.Device);
            }
        }

        Users = _appDbContext.Users.ToList();

        return Page();

    }

    public string GetSasToken(string key) {
        return _storageHelper.GetSasToken(Project.ContainerOveride, key);
    }

}