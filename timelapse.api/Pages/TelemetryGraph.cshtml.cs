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
public class TelemetryGraphModel : PageModel
{
    private readonly ILogger<TelemetryGraphModel> _logger;
    private AppDbContext _appDbContext;

    public Device device {get; private set;}
    public string SasToken {get; private set;}

    public DateTime? LatestTelemetryDateTime {get; private set;}
    public DateTime? EarliestTelemetryDateTime {get; private set;}

    // private int _numberOfHoursToDisplay;

    //     get {
    //         return _numberOfHoursToDisplay;
    //     }
    // }

    public TelemetryGraphModel(ILogger<TelemetryGraphModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
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

    public IActionResult OnGet(int id, int? numberOfHoursToDisplay = null)
    {
        _logger.LogInformation("TelemetryGraph.OnGet");
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        _logger.LogInformation($"About to query latest telemetry.... {stopwatch.ElapsedMilliseconds}ms");

        LatestTelemetryDateTime = _appDbContext.Telemetry
            .Where(t => t.DeviceId == id)
            .OrderByDescending(t => t.Timestamp)
            .Select(t => t.Timestamp)
            .FirstOrDefault();

        if(LatestTelemetryDateTime==null || !LatestTelemetryDateTime.HasValue || LatestTelemetryDateTime.Value == DateTime.MinValue){
            return RedirectToPage("/NotFound");
        }

        DateTime cutOff = LatestTelemetryDateTime.Value.AddHours(-1 * NumberOfHoursToDisplay);

        _logger.LogInformation($"About to get device info, including telemetry and latest image.... {stopwatch.ElapsedMilliseconds}ms");

        var d = _appDbContext.Devices
            .Include(d => d.Telemetries.Where(t => t.Timestamp >= cutOff && t.DeviceId == id))
            .Include(d => d.Images.Where(i => i.Timestamp >= cutOff && i.DeviceId == id).OrderByDescending(i => i.Timestamp).Take(1))
            .AsSplitQuery()
            .FirstOrDefault(d => d.Id == id);

        _logger.LogInformation($"Retrieved get device info. {stopwatch.ElapsedMilliseconds}ms");

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        if(d.Telemetries.Count()==0){
            return RedirectToPage("/NotFound");
        }

        _logger.LogInformation($"About to get latest telemetry data... {stopwatch.ElapsedMilliseconds}ms");

        EarliestTelemetryDateTime = d.Telemetries
            .OrderBy(t => t.Timestamp)
            .Select(t => t.Timestamp)
            .FirstOrDefault();

        _logger.LogInformation($"Retrieved latest telemetry data. {stopwatch.ElapsedMilliseconds}ms");

        return Page();

    }
}