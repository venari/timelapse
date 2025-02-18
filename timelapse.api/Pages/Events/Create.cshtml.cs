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
using Microsoft.AspNetCore.Mvc.Rendering;

namespace timelapse.api.Pages.Events;

[Authorize]
[AllowAnonymous]
public class CreateModel : PageModel
{
    private readonly ILogger<CreateModel> _logger;
    private AppDbContext _appDbContext;

    public Device device {get; set;}
    public string SasToken {get; set;}

    [BindProperty]
    public DateTime MinTimestamp {get; set; }
    [BindProperty]
    public DateTime MaxTimestamp {get; set; }

    // [BindProperty]
    // public DateTime StartTime {get; set;}
    // [BindProperty]
    // public DateTime EndTime {get; set;}

    [BindProperty]
    public DateTime StartTimeUTC {get; set;}
    [BindProperty]
    public DateTime EndTimeUTC {get; set;}
    
    // [BindProperty]
    // [Required]
    // public EventType EventType {get; set;}

    // [BindProperty]
    // [Required]
    // public int SelectedEventTypeId {get; set;}

    // public List<SelectListItem> EventTypes {
    //     get {
    //         return _appDbContext.EventTypes.OrderBy(et => et.Name).Select(et => new SelectListItem($"{et.Name}", et.Id.ToString())).ToList();
    //     }
    // }

    [BindProperty]
    [Required]
    public List<EventType> SelectedEventTypes {get; set;}
    [BindProperty]
    [Required(ErrorMessage = "At least one Event Type must be selected.")]
    public string SelectedEventTypeIdsCSV {get; set;}

    public List<EventType> EventTypes {
        get {
            return _appDbContext.EventTypes.OrderBy(et => et.Name).ToList();
        }
    }

    [BindProperty]
    [Required]
    public string Description {get; set;}


    [BindProperty]
    public DateTime InitialTimestamp {get; set;}

    public CreateModel(ILogger<CreateModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        StorageHelper storageHelper;
        storageHelper = new StorageHelper(configuration, logger, memoryCache);
        SasToken = storageHelper.SasToken;
    }


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

        InitialTimestamp = image.Timestamp;
        EndTimeUTC = StartTimeUTC = InitialTimestamp;
        // DeviceId = device.Id;
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


    public async Task<IActionResult> OnPostAsync(int imageId)
    {
        var user = GetCurrentUser();

        // _logger.LogInformation($"StartTime: {StartTime.ToString()} Kind: {StartTime.Kind} UTC: {StartTime.ToUniversalTime().ToString()}.  StartTimeUTC: {StartTimeUTC.ToString()} ");
        // _logger.LogInformation($"EndTime: {EndTime.ToString()} - {EndTime.ToUniversalTime().ToString()}.  EndTimeUTC: {EndTimeUTC.ToString()}");

        if(user==null){
            return Redirect("/Identity/Account/Login");
        }

        var image = _appDbContext.Images
            .Include(i => i.Device)
            .FirstOrDefault(i => i.Id == imageId);

        if(image==null){
            return RedirectToPage("/NotFound");
        }

        device = image.Device;
        InitialTimestamp = image.Timestamp;

        if (StartTimeUTC != null && EndTimeUTC != null && StartTimeUTC > EndTimeUTC){
            ModelState.AddModelError("StartTimeUTC", "End Time is not later than Start Time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Event newEvent = new Event();
        newEvent.LastEditedByUserId = newEvent.CreatedByUserId = user.Id;
        newEvent.DeviceId = device.Id;
        newEvent.StartTime = StartTimeUTC;
        newEvent.EndTime = EndTimeUTC;
        newEvent.Description = Description;

        newEvent.StartImage = _appDbContext.Images.OrderBy(i => i.Timestamp).FirstOrDefault(i => i.DeviceId == device.Id && i.Timestamp >= newEvent.StartTime);
        newEvent.EndImage = _appDbContext.Images.OrderByDescending(i => i.Timestamp).FirstOrDefault(i => i.DeviceId == device.Id && i.Timestamp <= newEvent.EndTime);

        // newEvent.EventTypeId = 1;
        foreach(var eventTypeId in SelectedEventTypeIdsCSV.Split(",")){
            var eventType = _appDbContext.EventTypes.FirstOrDefault(et => et.Id == int.Parse(eventTypeId));
            if(eventType!=null){
                newEvent.EventTypes.Add(eventType);
            }
        }

        _appDbContext.Events.Add(newEvent);
        await _appDbContext.SaveChangesAsync();

        // Add Event Type(s) to just created Event to avoid error:
        // PostgresException: 23503: insert or update on table "events" violates foreign key constraint "fk_events_event_types_event_type_id" DETAIL: Key (event_type_id)=(0) is not present in table "event_types".

        // _appDbContext.Events.Update(newEvent);
        // await _appDbContext.SaveChangesAsync();

        return RedirectToPage("Detail", new { eventId = newEvent.Id });
    }
}