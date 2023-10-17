using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using timelapse.api.Areas.Identity.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace timelapse.api.Pages
{
    [Authorize]
    public class ManageProjectModel : PageModel
    {
        private readonly AppDbContext _appDbContext;
        private readonly ILogger<ManageProjectModel> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
                
        private Project Project { get; set; }

        [BindProperty]
        public string ProjectName { get; set; }

        [BindProperty]
        public int ProjectId { get; private set;}

        [BindProperty]
        // public Container? ContainerOveride {get; set;}
        public int ContainerOverideId {get; set;}

        public List<SelectListItem> ContainerIds {get; set;}

        public List<DeviceProjectContract> DeviceProjectContracts;
        public List<Device> Devices;

        [BindProperty]
        public int RetOrganisationId {get; set;}

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

            ProjectName = Project.Name;
            ProjectId = Project.Id;

            RetOrganisationId = Project.OrganisationId;

            ContainerIds = Project.Organisation.Containers.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
            ContainerIds.Insert(0, new SelectListItem("None", "-1"));

            DeviceProjectContracts = _appDbContext.DeviceProjectContracts.Where(c => c.ProjectId == projectId).ToList();
            Devices = _appDbContext.Devices.Where(d => d.Retired==false).ToList();

            return true;
        }

        public IActionResult OnGet(int projectId)
        {
            if(!LoadProject(projectId)){
                return NotFound($"No project with Id {projectId}");
            }

            ContainerOverideId = Project.ContainerOveride?.Id ?? -1;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int projectId)
        {
            if(!LoadProject(projectId)){
                return NotFound($"No project with Id {projectId}");
            }

            if (! ModelState.IsValid)
            {
                return Page();
            }

            Project.Name = ProjectName;
            if(ContainerOverideId!=-1){
                Project.ContainerOveride = _appDbContext.Containers.FirstOrDefault(c => c.Id == ContainerOverideId);
            } else {
                Project.ContainerOveride = null;
            }
            _appDbContext.Projects.Update(Project);
            
            await _appDbContext.SaveChangesAsync();

            return Redirect($"ManageOrganisation?Id={Project.OrganisationId}");
        }
    }
}
