using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Filters;

namespace timelapse.api.Pages;

[Authorize]
[AllowAnonymous]
public class CreateEventModel : PageModel
{
    private readonly ILogger<CreateEventModel> _logger;
    private AppDbContext _appDbContext;

    public Device device {get; private set;}
    public string SasToken {get; private set;}

    // public DateTime oldestImageTimestamp {get; private set;}
    // public DateTime newestImageTimestamp {get; private set;}
    // public Image[] imagesLast24Hours {get; private set;}

    public DateTime InitialTimestamp {get; private set;}

    public CreateEventModel(ILogger<CreateEventModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        StorageHelper storageHelper;
        storageHelper = new StorageHelper(configuration, logger, memoryCache);
        SasToken = storageHelper.SasToken;
    }

    // [BindProperty]
    // public int NumberOfHoursToDisplay {get; set; }

    [BindProperty]
    public DateTime MinTimestamp {get; set; }
    public DateTime MaxTimestamp {get; set; }

    // public IActionResult OnGet(int deviceId, DateTime intialDateTime)
    public IActionResult OnGet(int imageId)
    // public IActionResult OnGet(int deviceId, int intialDateTime)
    {
        // Unusual authentication here - want to accept logged in users, and the Third Party key
        // Duplication of logic in ThirdPartyApiKeyAuthAttribute

        const string ApiKeyHeaderName = "api-key";
        if(HttpContext.User==null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated)
        {
            if (!HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var potentialApiKey))
            {
                // Not in headers? Let's try in query string?
                if(!HttpContext.Request.Query.TryGetValue(ApiKeyHeaderName, out potentialApiKey))
                {
                    return Redirect("/Identity/Account/Login");
                    // return Unauthorized();
                    // return new UnauthorizedResult();
                }
            }

            var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = configuration.GetValue<string>("ThirdParty_ApiKey");

            if(apiKey==null){
                return Redirect("/Identity/Account/Login");
            }

            if(!apiKey.Equals(potentialApiKey))
            {
                return Redirect("/Identity/Account/Login");
            }
        }

        var image = _appDbContext.Images
            .Include(i => i.Device)
            .FirstOrDefault(i => i.Id == imageId);

        if(image==null){
            return RedirectToPage("/NotFound");
        }

        device = image.Device;

        var minAndMaxTimestamps = _appDbContext.Images
            .Where(t => t.DeviceId == device.Id)
            .GroupBy(t => t.DeviceId)
            // .OrderByDescending(t => t.Timestamp)
            .Select(t => new{
                MinTimestamp = t.Min(i => i.Timestamp),
                MaxTimestamp = t.Max(i => i.Timestamp)
            })
            .FirstOrDefault();

        if(minAndMaxTimestamps==null || minAndMaxTimestamps.MinTimestamp == DateTime.MinValue || minAndMaxTimestamps.MaxTimestamp == DateTime.MinValue){
            return RedirectToPage("/NotFound");
        }

        InitialTimestamp = image.Timestamp;
        MinTimestamp = minAndMaxTimestamps.MinTimestamp;
        MaxTimestamp = minAndMaxTimestamps.MaxTimestamp;

        return Page();
    }
}