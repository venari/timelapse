using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Areas.Identity.Data;
using timelapse.infrastructure;
using timelapse.core.models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace timelapse.api.Pages
{

    public class CreateCameraAllocationModel : PageModel
    {
        private readonly ILogger<CreateCameraAllocationModel> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private AppDbContext _appDbContext;
        
        [BindProperty]
        public List<Device> Devices { get; set; }

        public CreateCameraAllocationModel(
            ILogger<CreateCameraAllocationModel> Logger,
            UserManager<AppUser> UserManager,
            SignInManager<AppUser> SignInManager,
            AppDbContext AppDbContext)
        {
            _logger = Logger;
            _userManager = UserManager;
            _signInManager = SignInManager;
            _appDbContext = AppDbContext;
        }

        public Project Project { get; private set; }

        [BindProperty]
        public DateTime? StartTimeUTC {get; set;}
        [BindProperty]
        public DateTime? EndTimeUTC {get; set;}

        [BindProperty]
        public int DeviceId {get; set;}

        [BindProperty]
        public List<SelectListItem> DeviceIds {get; set;}

        // [BindProperty]
        public DeviceProjectContract DeviceProjectContract { get; set; }

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

            Devices = _appDbContext.Devices.Where(d => d.Retired==false).ToList();
            DeviceIds = Devices.Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToList();
            DeviceIds.Insert(0, new SelectListItem("Please select", "-1"));

            return true;
        }


        public ActionResult OnGet(int projectId)
        {
            if(!LoadProject(projectId)){
                return NotFound($"No project with ID {projectId}");
            }

            StartTimeUTC = DateTime.UtcNow;
            EndTimeUTC = null;

            DeviceId = -1;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int projectId)
        {
            if(!LoadProject(projectId)){
                return NotFound($"No project with ID {projectId}");
            }

            if(StartTimeUTC==null){
                ModelState.AddModelError("StartTimeUTC", "Please supply Start Time");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            DeviceProjectContract = new DeviceProjectContract
            {
                ProjectId = projectId,
                StartDate = StartTimeUTC.Value,
                EndDate = EndTimeUTC
            };

            _appDbContext.DeviceProjectContracts.Add(DeviceProjectContract);
            await _appDbContext.SaveChangesAsync();

            return RedirectToPage("/ManageProject", new {projectId = Project.Id});
        }
    }
}