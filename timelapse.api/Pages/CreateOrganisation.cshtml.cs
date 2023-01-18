using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.api.Areas.Identity.Data;
using timelapse.core.models;
using Microsoft.AspNetCore.Identity;
using timelapse.infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace timelapse.api.Pages
{
    [Authorize]
    public class CreateOrganisationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<CreateOrganisationModel> _logger;
        private AppDbContext _appDbContext;

        public CreateOrganisationModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILogger<CreateOrganisationModel> logger,
            AppDbContext appDbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _appDbContext = appDbContext;

            organisation = new Organisation();
        }
        
        [BindProperty]
        public Organisation organisation { get; set; }
        
        public IActionResult OnGet()
        {
            return Page();
        }
        
        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var UserId = _userManager.GetUserId(User);
                if (UserId == null) // Might not need this
                {
                    return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
                }

                _logger.LogInformation($"Organisation created by user {_userManager.GetUserName(User)}");
                _appDbContext.Organisations.Add(organisation);
                
                await _appDbContext.SaveChangesAsync(); // Looks like organisation.Id is only updated after saving changes

                var OwnerUserEntry = new OrganisationUserJoinEntry{UserId=UserId, OrganisationId=organisation.Id, OrganisationAdmin=true, OrganisationOwner=true, CreationDate=DateTime.UtcNow};
                _appDbContext.OrganisationUserJoinEntry.Add(OwnerUserEntry);

                await _appDbContext.SaveChangesAsync();
                return RedirectToPage("./Organisations");
            }
            return Page();
        }
    }
}
