using Microsoft.AspNetCore.Mvc;
using timelapse.infrastructure;
using timelapse.core.models;
using timelapse.api.Areas.Identity.Data;

namespace timelapse.api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : Controller
    {
        public ProjectController(AppDbContext appDbContext, ILogger<ProjectController> logger){
            _appDbContext = appDbContext;
            _logger = logger;

            _logger.LogCritical("ProjectController::AddMockDevice exists, this is an insecure test function, remove once it is no longer needed");
        }

        private static AppDbContext _appDbContext;
        private static ILogger _logger;
        

        [HttpPost("AddMockDevice")]
        public async Task<ActionResult<String>> AddMockDevice(int ProjectId) // DEVDO this should be removed once testing is complete, or moved to a site admin page
        {
            _logger.LogInformation("ProjectController::AddMockDevice");
            _logger.LogWarning("This should be removed when testing is complete");
            _logger.LogWarning("Dangerous Test ProjectController::AddMockDevice exists");
            Device d = new Device{SerialNumber=$"Insecure Test {DateTime.UtcNow.ToString()}", Name=$"Test {DateTime.UtcNow.ToString()}", Description="test"};
            _appDbContext.Devices.Add(d);
            _appDbContext.SaveChanges();

            DeviceProjectContract dpc = new DeviceProjectContract{Project=_appDbContext.Projects.First(p => p.Id == ProjectId), StartDate=DateTime.UtcNow, EndDate=DateTime.UtcNow.AddDays(5), Device=d};
            _appDbContext.DeviceProjectContracts.Add(dpc);
            _appDbContext.SaveChanges();

            return "SUCCEED: Mock Device Added";
        }

        // DEVDO move these functions from project and organisation controllers into some shared file
        public static async Task<bool> CurrentUserHasAdminPermissions(HttpRequest Request, int OrganisationId)
        {
            validateUserOrganisationJoinEntries();
            var currentUserId = (await GetCurrentUserFromRequest(Request)).Id;
            return _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == OrganisationId && e.UserId == currentUserId && e.OrganisationAdmin);
        }
        
        public static async Task<bool> CurrentUserHasOwnerPermissions(HttpRequest Request, int OrganisationId)
        {
            validateUserOrganisationJoinEntries();
            var currentUserId = (await GetCurrentUserFromRequest(Request)).Id;
            return _appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == OrganisationId && e.UserId == currentUserId && e.OrganisationOwner);
        }
        
        public static async void validateUserOrganisationJoinEntries()
        {
            foreach (var entry in _appDbContext.OrganisationUserJoinEntry.ToList())
            {
                if (! _appDbContext.Organisations.Any(o => o.Id == entry.OrganisationId))
                {
                    _logger.LogError($"Organisation {entry.OrganisationId} does not exist, but OrganisationUserJoinEntry {entry.Id} refers to it");
                }
                
                if (! _appDbContext.Users.Any(u => u.Id == entry.UserId))
                {
                    _logger.LogError($"User {entry.UserId} does not exist, but OrganisationUserJoinEntry {entry.Id} refers to it");
                }
            }
        }

        public static async Task<AppUser> GetCurrentUserFromRequest(HttpRequest Request){
            try{

                if(Request.HttpContext.User== null || Request.HttpContext.User.Identity == null || !Request.HttpContext.User.Identity.IsAuthenticated){
                    _logger.LogWarning("User sent Request to OrganisationController without being authenticated");
                    return null;
                }

                var currentUser = _appDbContext.Users.SingleOrDefault(u => u.UserName == Request.HttpContext.User.Identity.Name);

                if(currentUser == null){
                    _logger.LogError($"GetCurrentUserFromRequest returned null user");
                }

                return currentUser;
            }
            catch(Exception ex){
                _logger.LogError(ex.Message);
                if(ex.InnerException != null){
                    _logger.LogError(ex.InnerException.Message);
                }
                throw;
            }
        }
    }    
}