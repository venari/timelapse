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
    public class ManageOrganisationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<ManageOrganisationModel> _logger;
        public List<Organisation> Organisations;
        private AppDbContext _appDbContext;

        public ManageOrganisationModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILogger<ManageOrganisationModel> logger,
            AppDbContext appDbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _appDbContext = appDbContext;
            
            Organisations = _appDbContext.Organisations
                // TODO: use organisation JoinEntries to return only organisations that the user is a member of
                //.Include(o => o.OrganisationUserJoinEntries.Where(t => t.Timestamp >= cutOff))
                //.Include(d => d.Images.OrderByDescending(i => i.Timestamp).Take(1))
                .AsSplitQuery()
                .ToList();
        }

        public IActionResult OnGet(int OrganisationId)
        {
            var user = _userManager.GetUserAsync;
            return Page();
        }
    }
}
