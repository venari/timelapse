
using Microsoft.AspNetCore.Mvc;
using timelapse.infrastructure;
using timelapse.core.models;
using timelapse.api.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;

namespace timelapse.api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class DebugController : Controller
    {
        public DebugController(AppDbContext appDbContext, ILogger<DebugController> logger){
            _appDbContext = appDbContext;
            _logger = logger;
        }

        private static AppDbContext _appDbContext;
        private static ILogger<DebugController> _logger;
        

        [HttpPost("changeOrganisationSoftDeleteFlag")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<String>> changeOrganisationSoftDeleteFlag(int OrganisationId, bool Value)
        {
            if (! User.IsInRole("Admin"))
            {
                _logger.LogCritical($"A non admin user has accessed debug controller method changeOrganisationSoftDeleteFlag");
            }
            
            var currentUser = await GetCurrentUserFromRequest(Request);
            if (! _appDbContext.Organisations.Any(o => o.Id == OrganisationId))
            {
                _logger.LogWarning($"User {currentUser.Id} (\"{currentUser.UserName}\") attempted to change soft delete flag of non-existant organisation with Id {OrganisationId}");
                return $"FAIL: No Organisation With Id {OrganisationId}";
            }

            var org = _appDbContext.Organisations.First(o => o.Id == OrganisationId);
            org.SoftDeleteFlag = Value;
            _appDbContext.SaveChanges();
            return "SUCCEED: Updated Organisation Flag";
        }

        [HttpPost("changePermission")]
        public async Task<ActionResult<String>> ChangePermission(int EntryId, string Permission, bool Value)
        {
            _logger.LogInformation("DebugController::ChangePermission");
            if (! User.IsInRole("Admin"))
            {
                return "FAIL: Authentication Error";
            }
            
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.Id == EntryId))
            {
                return "FAIL: Database OrganisationUserJoinEntry Error, this entry does not exist";
            }
            
            var currentUser = await GetCurrentUserFromRequest(Request);
            var joinEntry = _appDbContext.OrganisationUserJoinEntry.First(e => e.Id == EntryId);
            _logger.LogWarning($"User {currentUser.Id} (\"{currentUser.UserName}\") changed permission \"{Permission}\" of user {joinEntry.UserId} (\"{_appDbContext.Users.First(u => u.Id == joinEntry.UserId).UserName}\") to {Value}");
            
            switch (Permission)
            {
                case "Admin":
                    joinEntry.OrganisationAdmin = Value;
                    break;
                
                case "Owner":
                    joinEntry.OrganisationOwner = Value;
                    break;

                default:
                    _logger.LogWarning($"Unknown permission {Permission} in ChangePermission request made by user {currentUser.Id} (\"{currentUser.UserName}\") to user {joinEntry.UserId} (\"{_appDbContext.Users.First(u => u.Id == joinEntry.UserId).UserName}\") in Organisation {joinEntry.OrganisationId} (\"{_appDbContext.Organisations.First(o => o.Id == joinEntry.OrganisationId).Name}\")");
                    return "FAIL: Unknown Permission";
            }
            _appDbContext.SaveChanges();
            return "SUCCEED: Permission Changed";
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