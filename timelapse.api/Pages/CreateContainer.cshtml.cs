using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Areas.Identity.Data;
using timelapse.infrastructure;
using timelapse.core.models;
using Microsoft.AspNetCore.Mvc.Rendering;

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
        public int RetOrganisationId {get; set;}


        // public List<ContainerProvider> ContainerProviders {get; set; } = new List<ContainerProvider>(){core.models.ContainerProvider.Azure_Blob, core.models.ContainerProvider.AWS_S3};

        public List<SelectListItem> ContainerProviders {
            get {
                return new List<SelectListItem>(){
                    new SelectListItem(core.models.ContainerProvider.Azure_Blob.ToString(), core.models.ContainerProvider.Azure_Blob.ToString()),
                    new SelectListItem(core.models.ContainerProvider.AWS_S3.ToString(), core.models.ContainerProvider.AWS_S3.ToString())
                };
            }
        }


        #region Azure specific controls
        [BindProperty]
        public string StorageAccountName {get; set; }
        [BindProperty]
        public string ConnectionString {get; set; }
        #endregion

        #region AWS specific controls
        [BindProperty]
        public string Region { get; set; }
        [BindProperty]
        public string BucketName { get; set; }
        [BindProperty]
        public string AccessKey {get; set; }
        [BindProperty]
        public string SecretKey { get; set; }
        #endregion


        
        public ActionResult OnGet(int OrganisationId)
        {
            RetOrganisationId = OrganisationId;

            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == OrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                return NotFound("You are not authenticated to add containers to this organisation");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int OrganisationId)
        {
            RetOrganisationId = OrganisationId;

            if(ContainerProvider == ContainerProvider.Azure_Blob)
            {
                if(String.IsNullOrEmpty(StorageAccountName) || String.IsNullOrEmpty(ConnectionString))
                {
                    ModelState.AddModelError("StorageAccountName", "StorageAccountName and Connection String are required");
                    return Page();
                }
            }
            else if(ContainerProvider == ContainerProvider.AWS_S3)
            {
                if(String.IsNullOrEmpty(Region) || String.IsNullOrEmpty(BucketName) || String.IsNullOrEmpty(AccessKey) || String.IsNullOrEmpty(SecretKey))
                {
                    ModelState.AddModelError("Region", "Region, BucketName, AccessKey and SecretKey are required");
                    return Page();
                }
            }
            else
            {
                ModelState.AddModelError("ContainerProvider", "ContainerProvider not implemented");
                return Page();
            }

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

                    ((Container_Azure_Blob)container).ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={AccessKey};EndpointSuffix=core.windows.net";
                    ((Container_Azure_Blob)container).StorageAccountName = StorageAccountName;
                    break;
                case ContainerProvider.AWS_S3:
                    container = new Container_AWS_S3();
                    ((Container_AWS_S3)container).BucketName=BucketName;
                    ((Container_AWS_S3)container).Region=Region;
                    ((Container_AWS_S3)container).AccessKey=AccessKey;
                    ((Container_AWS_S3)container).SecretKey=SecretKey;
                    break;
                default:
                    throw new Exception($"ContainerProvider {ContainerProvider} not implemented!");
            }   

            container.Name = ContainerName;

            container.OwnerOrganisation = _appDbContext.Organisations.First(o => o.Id == OrganisationId);
            container.Name = ContainerName;
            _appDbContext.Containers.Add(container);
            
            await _appDbContext.SaveChangesAsync();

            return Redirect($"/ManageOrganisation?Id={OrganisationId}");
        }
    }
}
