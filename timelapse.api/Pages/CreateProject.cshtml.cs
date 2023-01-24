using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Areas.Identity.Data;
using timelapse.infrastructure;
using timelapse.core.models;

namespace timlapse.api.Pages
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
        [BindProperty]
        public String ProjectName { get; set; }
        public ActionResult OnGet(int OrganisationId)
        {
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == OrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                return NotFound("You are not authenticated to add projects to this organisation");
            }

            return Page();
        }

        public int RetOrganisationId;
        public async Task<IActionResult> OnPostAsync(int OrganisationId)
        {
            RetOrganisationId = OrganisationId;
            if (! ModelState.IsValid)
            {
                return Page();
            }
            
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == OrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                _logger.LogWarning($"Unauthorised project creation attempt for organisation {OrganisationId} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")")
                return NotFound($"Not authorised to create projects for organisation {OrganisationId}");
            }

            _logger.LogInformation($"Project created by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");

            project = new Project();
            project.Organisation = _appDbContext.Organisations.First(o => o.Id == OrganisationId);
            project.Name = ProjectName;
            _appDbContext.Projects.Add(project);
            
            await _appDbContext.SaveChangesAsync();

            return Redirect($"ManageProject?Id={project.Id}");
        }
    }
}
