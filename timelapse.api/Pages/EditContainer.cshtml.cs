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
    public class EditContainerModel : PageModel
    {
        private readonly ILogger<EditContainerModel> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private AppDbContext _appDbContext;
        
        public EditContainerModel(
            ILogger<EditContainerModel> Logger,
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

        #region Azure specific controls
        // [BindProperty]
        // public string? StorageAccountName {get; set; }
        [BindProperty]
        public string? Azure_ConnectionString {get; set; }
        #endregion

        #region AWS specific controls
        [BindProperty]
        public string? AWS_S3_Region { get; set; }
        [BindProperty]
        public string? AWS_S3_BucketName { get; set; }
        [BindProperty]
        public string? AWS_S3_AccessKey {get; set; }
        [BindProperty]
        public string? AWS_S3_SecretKey { get; set; }
        #endregion


        
        public ActionResult OnGet(int containerId)
        {
            container = _appDbContext.Containers.FirstOrDefault(c => c.Id == containerId);
        
            if(container==null)
            {
                return NotFound($"Container {containerId} not found");
            }

            RetOrganisationId = container.OwnerOrganisationId;

            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == container.OwnerOrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                return NotFound("You are not authenticated to edit containers to this organisation");
            }

            ContainerName = container.Name;
            if(container is Container_Azure_Blob){
                    
                    var accessKeyStartIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf("AccountKey=") + "AccountKey=".Length;
                    var accessKeyEndIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf(";", accessKeyStartIndex);
                    var accessKey = ((Container_Azure_Blob)container).ConnectionString.Substring(accessKeyStartIndex, accessKeyEndIndex - accessKeyStartIndex);

                    Azure_ConnectionString = ((Container_Azure_Blob)container).ConnectionString.Replace(accessKey, "********");
                    ContainerProvider = ContainerProvider.Azure_Blob;
                    
                    // StorageAccountName = ((Container_Azure_Blob)container).StorageAccountName;
            } else {
                if(container is Container_AWS_S3){
                    AWS_S3_BucketName = ((Container_AWS_S3)container).BucketName;
                    AWS_S3_Region = ((Container_AWS_S3)container).Region;
                    AWS_S3_AccessKey = ((Container_AWS_S3)container).AccessKey;
                    AWS_S3_SecretKey = ((Container_AWS_S3)container).SecretKey;

                    ContainerProvider = ContainerProvider.AWS_S3;
                } else 
                {
                    throw new Exception($"ContainerProvider {ContainerProvider} not implemented!");
                }
            }   

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int containerId)
        {
            container = _appDbContext.Containers.FirstOrDefault(c => c.Id == containerId);
            if(container==null)
            {
                return NotFound($"Container {containerId} not found");
            }

            RetOrganisationId = container.OwnerOrganisationId;

            if(ContainerProvider == ContainerProvider.Azure_Blob)
            {
                if(String.IsNullOrEmpty(Azure_ConnectionString))
                {
                    ModelState.AddModelError("ConnectionString", "Connection String are required");
                }
            }
            else if(ContainerProvider == ContainerProvider.AWS_S3)
            {
                if(String.IsNullOrEmpty(AWS_S3_Region))
                {
                    ModelState.AddModelError("Region", "Region is required");
                }
                if(String.IsNullOrEmpty(AWS_S3_BucketName))
                {
                    ModelState.AddModelError("BucketName", "BucketName is required");
                }
                if(String.IsNullOrEmpty(AWS_S3_AccessKey))
                {
                    ModelState.AddModelError("AccessKey", "AccessKey is required");
                }
                if(String.IsNullOrEmpty(AWS_S3_SecretKey))
                {
                    ModelState.AddModelError("SecretKey", "SecretKey is required");
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
            
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == container.OwnerOrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                _logger.LogWarning($"Unauthorised container creation attempt for organisation {container.OwnerOrganisationId} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
                return NotFound($"Not authorised to create containers for organisation {container.OwnerOrganisationId}");
            }

            _logger.LogInformation($"Container created by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");

            // Container container = null;

            if(container is Container_Azure_Blob){
                    var accessKeyStartIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf("AccountKey=") + "AccountKey=".Length;
                    var accessKeyEndIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf(";", accessKeyStartIndex);
                    var accessKey = ((Container_Azure_Blob)container).ConnectionString.Substring(accessKeyStartIndex, accessKeyEndIndex - accessKeyStartIndex);

                    ((Container_Azure_Blob)container).ConnectionString = Azure_ConnectionString.Replace("********", accessKey);

                    // ((Container_Azure_Blob)container).ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={AccessKey};EndpointSuffix=core.windows.net";
                    // ((Container_Azure_Blob)container).StorageAccountName = StorageAccountName;
            } else {
                if(container is Container_AWS_S3){
                    ((Container_AWS_S3)container).BucketName=AWS_S3_BucketName;
                    ((Container_AWS_S3)container).Region=AWS_S3_Region;
                    ((Container_AWS_S3)container).AccessKey=AWS_S3_AccessKey;
                    ((Container_AWS_S3)container).SecretKey=AWS_S3_SecretKey;
                } else 
                {
                    throw new Exception($"ContainerProvider {ContainerProvider} not implemented!");
                }
            }   

            container.Name = ContainerName;

            _appDbContext.Containers.Update(container);
            
            await _appDbContext.SaveChangesAsync();

            return Redirect($"/ManageOrganisation?Id={RetOrganisationId}");
        }
    }
}
