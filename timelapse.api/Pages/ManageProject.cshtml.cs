using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using timelapse.api.Areas.Identity.Data;

namespace timelapse.api.Pages
{
    [Authorize]
    public class ManageProjectModel : PageModel
    {
        private readonly AppDbContext _appDbContext;
        private readonly ILogger<ManageProjectModel> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        
        public Project Project { get; set; }
        public List<DeviceProjectContract> DeviceProjectContracts;
        public List<Device> Devices;

        public ManageProjectModel(
            AppDbContext appDbContext,
            ILogger<ManageProjectModel> logger,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            _appDbContext = appDbContext;
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
        }
        public IActionResult OnGet(int Id)
        {
            if (! _appDbContext.Projects.Any(p => p.Id == Id))
            {
                return NotFound($"No project with Id {Id}");
            }

            Project = _appDbContext.Projects.First(p => p.Id == Id);
            DeviceProjectContracts = _appDbContext.DeviceProjectContracts.Where(c => c.ProjectId == Id).ToList();
            Devices = _appDbContext.Devices.ToList();
            
            var userId = _userManager.GetUserId(User);
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == Project.OrganisationId && e.UserId == userId))
            {
                _logger.LogWarning($"Unauthorised access attempt on project {Project.Id} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
                return NotFound($"Not authorised to access project with id {Project.Id}");
            }

            return Page();
        }
    }
}
