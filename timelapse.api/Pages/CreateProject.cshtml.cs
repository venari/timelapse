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
    [Authorize]
    public class CreateProjectModel : PageModel
    {
        private readonly ILogger<CreateProjectModel> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private AppDbContext _appDbContext;
        
        public CreateProjectModel(
            ILogger<CreateProjectModel> Logger,
            UserManager<AppUser> UserManager,
            SignInManager<AppUser> SignInManager,
            AppDbContext AppDbContext)
        {
            _logger = Logger;
            _userManager = UserManager;
            _signInManager = SignInManager;
            _appDbContext = AppDbContext;
        }

        public Project project { get; set; }
        public Organisation Organisation {get; private set; }

        [BindProperty]
        public String ProjectName { get; set; }

        [BindProperty]
        // public Container? ContainerOveride {get; set;}
        public int ContainerOverideId {get; set;}

        [BindProperty]
        public List<SelectListItem> ContainerIds {get; set;}

        private bool LoadOrganisation(int organisationId){
            Organisation = _appDbContext.Organisations
                .Include(o => o.Containers)
                .FirstOrDefault(o => o.Id == organisationId);

            if(Organisation == null){
                return false;
            }
            
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == organisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                _logger.LogWarning($"Unauthorised project creation attempt for organisation {organisationId} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
            }

            ContainerIds = Organisation.Containers.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
            ContainerIds.Insert(0, new SelectListItem("None", "-1"));

            return true;
        }

        public ActionResult OnGet(int organisationId)
        {

            if(!LoadOrganisation(organisationId)){
                return NotFound($"No organisation with ID {organisationId}");
            }

            ContainerOverideId = -1;

            return Page();
        }

        public int RetOrganisationId;
        public async Task<IActionResult> OnPostAsync(int organisationId)
        {
            RetOrganisationId = organisationId;

            if(!LoadOrganisation(organisationId)){
                return NotFound($"No organisation with ID {organisationId}");
            }

            if (! ModelState.IsValid)
            {
                return Page();
            }
            
            _logger.LogInformation($"Project created by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");

            project = new Project();
            project.Organisation = _appDbContext.Organisations.First(o => o.Id == organisationId);
            project.Name = ProjectName;
            if(ContainerOverideId!=-1){
                project.ContainerOveride = _appDbContext.Containers.FirstOrDefault(c => c.Id == ContainerOverideId);
            }
            _appDbContext.Projects.Add(project);
            
            await _appDbContext.SaveChangesAsync();

            return Redirect($"ManageOrganisation?Id={organisationId}");
        }
    }
}
