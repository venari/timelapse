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
public class ImageViewModel : PageModel
{
    private readonly ILogger<ImageViewModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public Device device {get; private set;}
    public string SasToken {get; private set;}

    // public DateTime oldestImageTimestamp {get; private set;}
    // public DateTime newestImageTimestamp {get; private set;}
    public ImageSubset[] imagesLast24Hours {get; private set;}

    public ImageViewModel(ILogger<ImageViewModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        NumberOfHoursToDisplay = 24;
        _storageHelper = new StorageHelper(configuration, appDbContext, logger, memoryCache);
    }

    [BindProperty]
    public int NumberOfHoursToDisplay {get; set; }

    public IActionResult OnGet(int id, int? numberOfHoursToDisplay = null)
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


        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        DateTime latestImageDateTime = _appDbContext.Images
            .Where(t => t.DeviceId == id)
            .OrderByDescending(t => t.Timestamp)
            .Select(t => t.Timestamp)
            .FirstOrDefault();

        if(latestImageDateTime==null || latestImageDateTime == DateTime.MinValue){
            return RedirectToPage("/NotFound");
        }

        DateTime cutOff = latestImageDateTime.AddHours(-1 * NumberOfHoursToDisplay);

        var d = _appDbContext.Devices
            .Include(d => d.Images.Where(i =>i.Timestamp >= cutOff))
            .FirstOrDefault(d => d.Id == id);

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        imagesLast24Hours = d.Images.Select(i => new ImageSubset{Id = i.Id, Timestamp = i.Timestamp, BlobUri = i.BlobUri}).OrderBy(i => i.Timestamp).ToArray();
        if(imagesLast24Hours.Count()==0){
            return RedirectToPage("/NotFound");
        }

        SasToken = _storageHelper.SasToken(imagesLast24Hours[0].Id);


        return Page();

    }
}