using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Areas.Identity.Data;
using timelapse.infrastructure;
using timelapse.core.models;

namespace timelapse.api.Pages
{
    [Authorize]
    public class CreateContainerModel : PageModel
    {
        private readonly ILogger<CreateContainerModel> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private AppDbContext _appDbContext;
        
        public CreateContainerModel(
            ILogger<CreateContainerModel> Logger,
            UserManager<AppUser> UserManager,
            SignInManager<AppUser> SignInManager,
            AppDbContext AppDbContext)
        {
            _logger = Logger;
            _userManager = UserManager;
            _signInManager = SignInManager;
            _appDbContext = AppDbContext;
        }

        public Container container { get; set; }
        [BindProperty]
        public String ContainerName { get; set; }
        [BindProperty]
        public ContainerProvider ContainerProvider {get; set;} = ContainerProvider.AWS_S3;
        [BindProperty]
        public List<ContainerProvider> ContainerProviders {get; set; } = new List<ContainerProvider>(){core.models.ContainerProvider.Azure_Blob, core.models.ContainerProvider.AWS_S3};


        #region Azure specific controls
        [BindProperty]
        public string StorageAccountName {get; set; }
        [BindProperty]
        public string AccessKey {get; set; }
        [BindProperty]
        public string SecretKey { get; set; }
        #endregion

        #region AWS specific controls
        [BindProperty]
        public string Region { get; set; }
        [BindProperty]
        public string BucketName { get; set; }
        #endregion


        
        public ActionResult OnGet(int OrganisationId)
        {
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == OrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                return NotFound("You are not authenticated to add containers to this organisation");
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
                _logger.LogWarning($"Unauthorised container creation attempt for organisation {OrganisationId} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
                return NotFound($"Not authorised to create containers for organisation {OrganisationId}");
            }

            _logger.LogInformation($"Container created by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");

            Container container = null;

            switch(ContainerProvider)
            {
                case ContainerProvider.Azure_Blob:
                    container = new Container_Azure_Blob();
                    break;
                case ContainerProvider.AWS_S3:
                    container = new Container_AWS_S3();
                    break;
                default:
                    throw new Exception($"ContainerProvider {ContainerProvider} not implemented!");
            }   

            container.Name = ContainerName;

            container.OwnerOrganisation = _appDbContext.Organisations.First(o => o.Id == OrganisationId);
            container.Name = ContainerName;
            _appDbContext.Containers.Add(container);
            
            await _appDbContext.SaveChangesAsync();

            return Redirect($"ManageProject?Id={container.Id}");
        }
    }
}
