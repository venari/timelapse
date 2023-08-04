using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using timelapse.api.Filters;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Humanizer;

namespace timelapse.api.Pages.Events;

[Authorize]
[AllowAnonymous]
public class DetailModel : PageModel
{
    private readonly ILogger<DetailModel> _logger;
    private AppDbContext _appDbContext;

    public Device Device {get; set;}
    public Event Event {get; set;}
    public string SasToken {get; set;}

    public ImageSubset[] EventImages {get; private set;}

    public string EventDuration {
        get {
            var duration = Event.EndTime - Event.StartTime;
            return duration.Humanize();
        }
    }

    public string CreatedBy {
        get {
            var user = _appDbContext.Users.FirstOrDefault(u => u.Id == Event.CreatedByUserId);
            if(user!=null){
                return user.UserName;
            } else {
                return "[unknown]";
            }
        }
    }
    
    public DetailModel(ILogger<DetailModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        StorageHelper storageHelper;
        storageHelper = new StorageHelper(configuration, logger, memoryCache);
        SasToken = storageHelper.SasToken;
    }


    public IActionResult OnGet(int eventId)
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

        Event = _appDbContext.Events
            .Include(e => e.Device)
            .FirstOrDefault(e => e.Id == eventId);
            
        EventImages = _appDbContext.Images
            .Where(i => i.DeviceId == Event.DeviceId && i.Timestamp >= Event.StartTime.ToUniversalTime() && i.Timestamp <= Event.EndTime.ToUniversalTime())
            .OrderBy(i => i.Timestamp)
            .Select(i => new ImageSubset{
                Id = i.Id,
                Timestamp = i.Timestamp,
                BlobUri = i.BlobUri
            })
            .ToArray();

        if(Event==null){
            return RedirectToPage("/NotFound");
        }

        Device = Event.Device;

        // var minAndMaxTimestamps = _appDbContext.Images
        //     .Where(t => t.DeviceId == device.Id)
        //     .GroupBy(t => t.DeviceId)
        //     // .OrderByDescending(t => t.Timestamp)
        //     .Select(t => new{
        //         MinTimestamp = t.Min(i => i.Timestamp),
        //         MaxTimestamp = t.Max(i => i.Timestamp)
        //     })
        //     .FirstOrDefault();

        // if(minAndMaxTimestamps==null || minAndMaxTimestamps.MinTimestamp == DateTime.MinValue || minAndMaxTimestamps.MaxTimestamp == DateTime.MinValue){
        //     return RedirectToPage("/NotFound");
        // }

        // InitialTimestamp = image.Timestamp;
        // StartTime = InitialTimestamp;
        // EndTime = InitialTimestamp;
        // // DeviceId = device.Id;
        // MinTimestamp = minAndMaxTimestamps.MinTimestamp;
        // MaxTimestamp = minAndMaxTimestamps.MaxTimestamp;

        return Page();
    }

    private IdentityUser? GetCurrentUser(){

        if(Request.HttpContext.User== null || Request.HttpContext.User.Identity == null || !Request.HttpContext.User.Identity.IsAuthenticated){
            _logger.LogError($"Current User not found");
            return null;
        }

        var currentUser = _appDbContext.Users.SingleOrDefault(u => u.UserName == Request.HttpContext.User.Identity.Name);

        return currentUser;
    }
}