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
public class EditModel : PageModel
{
    private readonly ILogger<EditModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public Device device {get; set;}
    public string SasToken {get; set;}

    [BindProperty]
    public DateTime MinTimestamp {get; set; }
    [BindProperty]
    public DateTime MaxTimestamp {get; set; }

    [BindProperty]
    public DateTime StartTimeUTC {get; set;}
    public Uri EventStartImageBlobUri {get; private set;}
    [BindProperty]
    public DateTime EndTimeUTC {get; set;}
    
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

    public EditModel(ILogger<EditModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        _storageHelper = new StorageHelper(configuration, appDbContext, logger, memoryCache);
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

        var Event = _appDbContext.Events
            .Include(e => e.EventTypes)
            .Include(e => e.Device)
            .Include(e => e.StartImage)
            .FirstOrDefault(e => e.Id == eventId);

        if(Event==null){
            return RedirectToPage("/NotFound");
        }

        SasToken = _storageHelper.SasToken(Event.StartImageId);

        EventStartImageBlobUri = Event.StartImage.BlobUri;
        device = Event.Device;

        StartTimeUTC = Event.StartTime;
        EndTimeUTC = Event.EndTime;

        SelectedEventTypeIdsCSV = string.Join(",", Event.EventTypes.OrderBy(e => e.Name).Select(et => et.Id));
        SelectedEventTypes = Event.EventTypes;
        InitialTimestamp = StartTimeUTC;
        Description = Event.Description;

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


    public async Task<IActionResult> OnPostAsync(int eventId)
    {
        var user = GetCurrentUser();

        if(user==null){
            return Redirect("/Identity/Account/Login");
        }

        var existingEvent = _appDbContext.Events
            .Include(e => e.Device)
            .Include(e => e.EventTypes)
            .FirstOrDefault(e => e.Id == eventId);

        if(existingEvent==null){
            return RedirectToPage("/NotFound");
        }

        device = existingEvent.Device;
        
        if (StartTimeUTC != null && EndTimeUTC != null && StartTimeUTC > EndTimeUTC){
            ModelState.AddModelError("StartTimeUTC", "End Time is not later than Start Time.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        existingEvent.LastEditedByUserId = user.Id;
        existingEvent.LastEditedDate = DateTime.UtcNow;
        existingEvent.StartTime = StartTimeUTC;
        existingEvent.EndTime = EndTimeUTC;

        if(string.Join(",", existingEvent.EventTypes.OrderBy(e => e.Name).Select(e => e.Id)) != SelectedEventTypeIdsCSV){
            existingEvent.EventTypes.Clear();
            foreach(var eventTypeId in SelectedEventTypeIdsCSV.Split(",")){
                var eventType = _appDbContext.EventTypes.FirstOrDefault(et => et.Id == int.Parse(eventTypeId));
                if(eventType!=null){
                    existingEvent.EventTypes.Add(eventType);
                }
            }
        }

        existingEvent.Description = Description;

        existingEvent.StartImage = _appDbContext.Images.OrderBy(i => i.Timestamp).FirstOrDefault(i => i.DeviceId == device.Id && i.Timestamp >= existingEvent.StartTime);
        existingEvent.EndImage = _appDbContext.Images.OrderByDescending(i => i.Timestamp).FirstOrDefault(i => i.DeviceId == device.Id && i.Timestamp <= existingEvent.EndTime);

        _appDbContext.Events.Update(existingEvent);
        await _appDbContext.SaveChangesAsync();

        return RedirectToPage("Detail", new { eventId = existingEvent.Id });
    }
}