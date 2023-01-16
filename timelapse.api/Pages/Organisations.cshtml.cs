using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using timelapse.api.Areas.Identity.Data;
using timelapse.infrastructure;
using timelapse.core.models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace timelapse.api.Pages
{
    public class OrganisationsModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<OrganisationsModel> _logger;
        public List<Organisation> Organisations;
        public List<OrganisationUserJoinEntry> OrgUserJoins;
        public List<AppUser> Users;
        private AppDbContext _appDbContext;

        public OrganisationsModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILogger<OrganisationsModel> logger,
            AppDbContext appDbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _appDbContext = appDbContext;
            
            Organisations = _appDbContext.Organisations.ToList();
            OrgUserJoins = _appDbContext.OrganisationUserJoinEntry.ToList();
            Users = _appDbContext.Users.ToList();
        }

        public IActionResult OnGet()
        {
            return Page();
        }
    }
}
