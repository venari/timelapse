using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using timelapse.api.Areas.Identity.Data;

namespace timelapse.api{

    public class LoginRequest{
        public string Email {get; set;}
        public string Password {get; set;}
        public bool RememberMe {get; set;}
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase{

        public AuthController(SignInManager<AppUser> signInManager, ILogger<AuthController> logger){
            _signInManager = signInManager;
            _logger = logger;
        }

        private SignInManager<AppUser> _signInManager;
        private ILogger _logger;

        // Mirrors Areas/Identity/Pages/Account/Login.cshtml.cs's OnPostAsync, but returns
        // JSON instead of redirecting a page - same SignInManager, same cookie, so the
        // session this sets is interchangeable with the classic login page's.
        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginRequest request){
            var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, request.RememberMe, lockoutOnFailure: false);

            if(result.Succeeded){
                _logger.LogInformation("User logged in.");
                return new { email = request.Email };
            }

            if(result.RequiresTwoFactor){
                return new UnauthorizedObjectResult(new { error = "This account requires two-factor authentication. Please sign in from the classic login page." });
            }

            if(result.IsLockedOut){
                _logger.LogWarning("User account locked out.");
                return new UnauthorizedObjectResult(new { error = "Account locked out." });
            }

            return new UnauthorizedObjectResult(new { error = "Invalid login attempt." });
        }

        [AllowAnonymous]
        [HttpPost("Logout")]
        public async Task<ActionResult> Logout(){
            await _signInManager.SignOutAsync();
            return new OkResult();
        }

        [AllowAnonymous]
        [HttpGet("Me")]
        public ActionResult<object> Me(){
            if(User?.Identity == null || !User.Identity.IsAuthenticated){
                return new UnauthorizedResult();
            }

            return new { email = User.Identity.Name };
        }
    }
}
