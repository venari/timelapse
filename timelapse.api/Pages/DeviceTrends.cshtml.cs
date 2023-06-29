using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace timelapse.api.Pages;

[Authorize]
public class DeviceTrendsModel : PageModel
{
    private readonly ILogger<DeviceTrendsModel> _logger;
    private AppDbContext _appDbContext;

    public List<Device> devices {get; private set;}
    public string SasToken {get; private set;}

    // public DateTime? LatestTelemetryDateTime {get; private set;}
    // public DateTime? EarliestTelemetryDateTime {get; private set;}

    // private int _numberOfHoursToDisplay;

    //     get {
    //         return _numberOfHoursToDisplay;
    //     }
    // }

    public DeviceTrendsModel(ILogger<DeviceTrendsModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        NumberOfHoursToDisplay = 24;
        StorageHelper storageHelper;
        storageHelper = new StorageHelper(configuration, logger, memoryCache);
        SasToken = storageHelper.SasToken;
    }

    [BindProperty]
    public int NumberOfHoursToDisplay {get; set; }

    [BindProperty]
    public DateTime StartDate {get; set;}

    [BindProperty]
    public DateTime EndDate {get; set;}

    public IActionResult OnGet(int? numberOfHoursToDisplay = null)
    {
        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        EndDate = DateTime.UtcNow;
        StartDate = EndDate.AddHours(-1 * NumberOfHoursToDisplay);



        // LatestTelemetryDateTime = _appDbContext.Telemetry
        // .Where(t => t.Device.Retired == false && t.Timestamp >= EndDate && t.Timestamp <= StartDate)
        // .Max(t => t.Timestamp);

        if(_appDbContext.Telemetry.Any(t => t.Device.Retired == false && t.Timestamp >= StartDate && t.Timestamp <= EndDate) == false){
            return RedirectToPage("/NotFound");
        }
        
        // LatestTelemetryDateTime = _appDbContext.Devices
        //     .Include(d => d.Telemetries.OrderByDescending(t => t.Timestamp).Take(1))
        //     .Where(d => d.Retired == false)
        //     .Select(t => t.LatestTelemetryTimestamp)
        //     .FirstOrDefault();

        // if(LatestTelemetryDateTime==null || !LatestTelemetryDateTime.HasValue || LatestTelemetryDateTime.Value == DateTime.MinValue){
        //     return RedirectToPage("/NotFound");
        // }

        // DateTime cutOff = LatestTelemetryDateTime.Value.AddHours(-1 * NumberOfHoursToDisplay);
        // DateTime cutOff = StartDate;
        
        devices = _appDbContext.Devices
            // .Include(d => d.Telemetries.Where(t => t.Timestamp >= StartDate && t.Timestamp <= EndDate))
            .Where(d => d.Retired == false)
            .ToList();
            // .Include(d => d.Images.OrderByDescending(i => i.Timestamp).Take(1))
            // .AsSplitQuery()

        if(devices==null || devices.Count()==0){
            return RedirectToPage("/NotFound");
        }

        // EarliestTelemetryDateTime = devices
        // .Where(d => d.Telemetries.Count>0)
        // .Min(d => d.Telemetries.Min(t => t.Timestamp));

        return Page();

    }
}