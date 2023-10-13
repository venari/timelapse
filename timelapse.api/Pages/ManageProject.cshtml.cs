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

        public IActionResult OnGet(int Id)
        {
            if (! _appDbContext.Projects.Any(p => p.Id == Id))
            {
                return NotFound($"No project with Id {Id}");
            }

            var project = _appDbContext.Projects
                .Include(p => p.Organisation)
                .ThenInclude(o => o.Containers)
                .First(p => p.Id == Id);

            ProjectName = project.Name;
            ProjectId = project.Id;

            RetOrganisationId = project.OrganisationId;

            ContainerIds = project.Organisation.Containers.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
            ContainerIds.Insert(0, new SelectListItem("None", "-1"));
            ContainerOverideId = project.ContainerOveride?.Id ?? -1;

            DeviceProjectContracts = _appDbContext.DeviceProjectContracts.Where(c => c.ProjectId == Id).ToList();
            Devices = _appDbContext.Devices.ToList();
            
            var userId = _userManager.GetUserId(User);
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == project.OrganisationId && e.UserId == userId))
            {
                _logger.LogWarning($"Unauthorised access attempt on project {project.Id} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
                return NotFound($"Not authorised to access project with id {project.Id}");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int Id)
        {
            if (! ModelState.IsValid)
            {
                return Page();
            }

            var project = _appDbContext.Projects.FirstOrDefault(p => p.Id == Id);
            if(project==null){
                return NotFound($"No project with Id {ProjectId}");
            }

            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == project.OrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                _logger.LogWarning($"Unauthorised project update attempt for organisation {project.OrganisationId} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
                return NotFound($"Not authorised to update projects for organisation {project.OrganisationId}");
            }

            project.Name = ProjectName;
            if(ContainerOverideId!=-1){
                project.ContainerOveride = _appDbContext.Containers.FirstOrDefault(c => c.Id == ContainerOverideId);
            } else {
                project.ContainerOveride = null;
            }
            _appDbContext.Projects.Update(project);
            
            await _appDbContext.SaveChangesAsync();

            return Redirect($"ManageOrganisation?Id={project.OrganisationId}");
        }
    }
}
