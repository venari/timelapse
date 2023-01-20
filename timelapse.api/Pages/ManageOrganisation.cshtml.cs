using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using timelapse.api.Areas.Identity.Data;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace timelapse.api.Pages
{
    [Authorize]
    public class ManageOrganisationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<OrganisationsModel> _logger;
        public List<Organisation> Organisations;
        public List<OrganisationUserJoinEntry> OrgUserJoins;
        public List<AppUser> Users;
        public Organisation? Org;
        public Boolean UserIsAdmin;
        public Boolean UserIsOwner;
        public String UserId;
        private AppDbContext _appDbContext;

        public ManageOrganisationModel(
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

        public IActionResult OnGet(int Id)
        {
            UserId = _userManager.GetUserId(User);
            Org = Organisations.Where(o => o.Id == Id).FirstOrDefault();
            if (Org == null)
            {
                return NotFound($"No Organisation with ID {Id}");
            }


            var userOrgRelation = Org.OrganisationUserJoinEntries.Where(e => e.UserId == UserId);
            if (userOrgRelation.Count() == 0)
            {
                _logger.LogWarning($"Unauthorised access attempt to organisation with Id {Id} (\"{Org.Name}\") by user {UserId}");
                return NotFound($"Not authorised to access organisation with Id {Id}");
            }
            
            UserIsAdmin = userOrgRelation.First().OrganisationAdmin;
            UserIsOwner = userOrgRelation.First().OrganisationOwner;

            return Page();
        }
    }
}
