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
                    ((Container_Azure_Blob)container).ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={AccessKey};EndpointSuffix=core.windows.net";

                    // Convert string in the form "DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={AccessKey};EndpointSuffix=core.windows.net" to its StorageAccountName and AccessKey components
                    var storageAccountNameStartIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf("AccountName=") + "AccountName=".Length;
                    var storageAccountNameEndIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf(";", storageAccountNameStartIndex);
                    StorageAccountName = ((Container_Azure_Blob)container).ConnectionString.Substring(storageAccountNameStartIndex, storageAccountNameEndIndex - storageAccountNameStartIndex);

                    var accessKeyStartIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf("AccountKey=") + "AccountKey=".Length;
                    var accessKeyEndIndex = ((Container_Azure_Blob)container).ConnectionString.IndexOf(";", accessKeyStartIndex);
                    AccessKey = ((Container_Azure_Blob)container).ConnectionString.Substring(accessKeyStartIndex, accessKeyEndIndex - accessKeyStartIndex);
                    
                    StorageAccountName = ((Container_Azure_Blob)container).StorageAccountName;
            } else {
                if(container is Container_AWS_S3){
                    BucketName = ((Container_AWS_S3)container).BucketName;
                    Region = ((Container_AWS_S3)container).Region;
                    AccessKey = ((Container_AWS_S3)container).AccessKey;
                    SecretKey = ((Container_AWS_S3)container).SecretKey;
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
            
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == container.OwnerOrganisationId && e.UserId == _userManager.GetUserId(User) && e.OrganisationAdmin))
            {
                _logger.LogWarning($"Unauthorised container creation attempt for organisation {container.OwnerOrganisationId} by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");
                return NotFound($"Not authorised to create containers for organisation {container.OwnerOrganisationId}");
            }

            _logger.LogInformation($"Container created by user {_userManager.GetUserId(User)} (\"{_userManager.GetUserName(User)}\")");

            // Container container = null;

            if(container is Container_Azure_Blob){
                    ((Container_Azure_Blob)container).ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={AccessKey};EndpointSuffix=core.windows.net";
                    ((Container_Azure_Blob)container).StorageAccountName = StorageAccountName;
            } else {
                if(container is Container_AWS_S3){
                    ((Container_AWS_S3)container).BucketName=BucketName;
                    ((Container_AWS_S3)container).Region=Region;
                    ((Container_AWS_S3)container).AccessKey=AccessKey;
                    ((Container_AWS_S3)container).SecretKey=SecretKey;
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
