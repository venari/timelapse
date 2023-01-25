using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using timelapse.infrastructure;
using Microsoft.AspNetCore.Identity;
using timelapse.api.Areas.Identity.Data;
using timelapse.core.models;

namespace timelapse.api.Pages
{
    [Authorize(Roles="Admin")]
    public class debugModel : PageModel
    {
        private ILogger<debugModel> _logger;
        private AppDbContext _appDbContext;
        private UserManager<AppUser> _userManager;
        private SignInManager<AppUser> _signInManager;
        public debugModel(
            ILogger<debugModel> Logger,
            AppDbContext AppDbContext,
            UserManager<AppUser> UserManager,
            SignInManager<AppUser> SignInManager)
        {
            _logger = Logger;
            _appDbContext = AppDbContext;
            _userManager = UserManager;
            _signInManager = SignInManager;
        }
        
        public List<AppUser> Users;
        public List<Organisation> Organisations;
        public List<OrganisationUserJoinEntry> OrganisationUserJoinEntries;
        public ActionResult OnGet()
        {
            _logger.LogWarning($"User {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\") accessed debug page");
            Users = _appDbContext.Users.OrderBy(u => u.Id).ToList();
            Organisations = _appDbContext.Organisations.OrderBy(o => o.Id).ToList();
            OrganisationUserJoinEntries = _appDbContext.OrganisationUserJoinEntry.OrderBy(e => e.OrganisationId).ThenBy(e => e.UserId).ToList();

            return Page();
        }
    }
}
