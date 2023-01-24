using Microsoft.AspNetCore.Mvc;
using timelapse.infrastructure;
using timelapse.core.models;
using timelapse.api.Areas.Identity.Data;

namespace timelapse.api
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrganisationController : Controller
    {
        public OrganisationController(AppDbContext appDbContext, ILogger<OrganisationController> logger){
            _appDbContext = appDbContext;
            _logger = logger;
        }

        private static AppDbContext _appDbContext;
        private static ILogger _logger;
        
        public List<Organisation> Organisations; // DEVDO delete these
        public List<OrganisationUserJoinEntry> OrganisationUserJoinEntries;
        
        [HttpPost("AddUserToOrganisation")]
        public async Task<ActionResult<String>> AddUserToOrganisation(String UserEmail, int OrganisationId)
        {
            if (! await CurrentUserHasAdminPermissions(Request, OrganisationId))
            {
                _logger.LogWarning($"User attempted to add user \"{UserEmail}\" to Organisation with id {OrganisationId} (\"{_appDbContext.Organisations.First(o => o.Id == OrganisationId).Name}\" without admin permissions");
                return "FAIL: Authentication Error";
            }
            
            if (! _appDbContext.Users.Any(u => u.UserName == UserEmail))
            {
                return "FAIL: Database User Error, email may have been mistyped, or user may not have an account on this server";
            }
            var userId = _appDbContext.Users.First(u => u.UserName == UserEmail).Id;
            
            if (_appDbContext.OrganisationUserJoinEntry.Any(e => e.OrganisationId == OrganisationId && e.UserId == userId))
            {
                return "FAIL: User Already In Organisation";
            }
            var currentUser = await GetCurrentUserFromRequest(Request);
            
            _logger.LogInformation($"User {currentUser.Id}(\"{currentUser.UserName}\") added user {userId} (\"{UserEmail}\") to organisation {OrganisationId} (\"{_appDbContext.Organisations.First(o => o.Id == OrganisationId).Name}\")");

            _appDbContext.OrganisationUserJoinEntry.Add(new OrganisationUserJoinEntry {UserId=userId, OrganisationId = OrganisationId, CreationDate=DateTime.UtcNow});
            await _appDbContext.SaveChangesAsync();

            return "SUCCEED: User Added";
        }
        
        [HttpPost("ChangeUserPermission")]
        public async Task<ActionResult<String>> ChangeUserPermission(string UserId, int OrganisationId, bool Value, string PermissionName)
        {
            if (! await CurrentUserHasOwnerPermissions(Request, OrganisationId))
            {
                return "FAIL: Authentication Error";
            }
            
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.UserId == UserId && e.OrganisationId == e.OrganisationId))
            {
                // Catches:
                //  - User does not exist in database
                //  - Organisation does not exist in database
                //  - User is not in this organisation
                return "FAIL: Database User Error, this user is not in this organisation";
            }
            
            var currentUser = await GetCurrentUserFromRequest(Request);

            _logger.LogInformation($"User {currentUser.Id} (\"{currentUser.UserName}\") changed permission \"{PermissionName}\" of user {UserId} (\"{_appDbContext.Users.First(u => u.Id == UserId).UserName}\") to {Value}");
            
            var joinEntry = _appDbContext.OrganisationUserJoinEntry.First(e => e.UserId == UserId && e.OrganisationId == OrganisationId);
            switch (PermissionName)
            {
                case "Admin":
                    joinEntry.OrganisationAdmin = Value;
                    break;
                
                case "Owner":
                    joinEntry.OrganisationOwner = Value;
                    break;

                default:
                    _logger.LogWarning($"Unknown permission {PermissionName} in ChangeUserPermission request made by user {currentUser.Id} (\"{currentUser.UserName}\") to user {UserId} (\"{_appDbContext.Users.First(u => u.Id == UserId).UserName}\") in Organisation {OrganisationId} (\"{_appDbContext.Organisations.First(o => o.Id == OrganisationId).Name}\")");
                    return "FAIL: Unknown Permission";
            }
            _appDbContext.SaveChanges();
            return "SUCCEED: Permission Changed";
        }
        
        [HttpPost("RemoveUserFromOrganisation")]
        public async Task<ActionResult<String>> RemoveUserFromOrganisation(string UserId, int OrganisationId)
        {
            if (! await CurrentUserHasAdminPermissions(Request, OrganisationId)) {
                return "FAIL: Authentication Error";
            }
            
            if (! _appDbContext.OrganisationUserJoinEntry.Any(e => e.UserId == UserId && e.OrganisationId == OrganisationId))
            {
                return "FAIL: User Database Error, this user is not in this organisation";
            }
            
            var currentUser = await GetCurrentUserFromRequest(Request);
            var currentUserJoinEntry = _appDbContext.OrganisationUserJoinEntry.First(e => e.OrganisationId == OrganisationId && e.UserId == currentUser.Id);

            var removee = _appDbContext.Users.First(u => u.Id == UserId);
            var organisation = _appDbContext.Organisations.First(o => o.Id == OrganisationId);
            var removeeJoinEntry = _appDbContext.OrganisationUserJoinEntry.First(e => e.UserId == UserId && e.OrganisationId == OrganisationId);
            
            if (removeeJoinEntry.OrganisationAdmin && ! currentUserJoinEntry.OrganisationOwner)
            {
                return "FAIL: Permission Error, Users with Admin permissions can only be removed by users with Owner permissions";
            }
            
            if (removee.Id == currentUser.Id)
            {
                return "FAIL: you cannot remove yourself from the organisation";
            }
            
            _logger.LogInformation($"User {currentUser.Id} (\"{currentUser.UserName}\") removed user {removee.Id} (\"{removee.UserName}\") from organisation {organisation.Id} (\"{organisation.Name}\")");
            _appDbContext.OrganisationUserJoinEntry.Remove(removeeJoinEntry);
            _appDbContext.SaveChanges();

            return "SUCCEED: User Removed";
        }
        
        [HttpPost("DeleteOrganisation")]
        public async Task<ActionResult<String>> DeleteOrganisation(int OrganisationId)
        {
            if (! await CurrentUserHasOwnerPermissions(Request, OrganisationId))
            {
                return "FAIL: Authentication Error";
            }

            var organisation = _appDbContext.Organisations.First(o => o.Id == OrganisationId);
            
            if (organisation.softDeleteFlag)
            {
                return "FAIL: organisation already soft-deleted";
            }

            organisation.softDeleteFlag = true;
            _appDbContext.SaveChanges();
            return "SUCCEED: organisation soft-deleted";
        }
        
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