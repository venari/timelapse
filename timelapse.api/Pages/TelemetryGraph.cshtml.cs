using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using NuGet.Protocol.Core.Types;

namespace timelapse.api.Pages;

[Authorize]
public class TelemetryGraphModel : PageModel
{
    private readonly ILogger<TelemetryGraphModel> _logger;
    private AppDbContext _appDbContext;

    public Device device {get; private set;}
    public string SasToken {get; private set;}

    public DateTime StartDate {get; private set;}
    public DateTime EndDate {get; private set;}

    // public DateTime? LatestTelemetryDateTime {get; private set;}
    // public DateTime? EarliestTelemetryDateTime {get; private set;}
    // public DateTime? TargetLatestTelemetryDateTime {get; private set;}
    // public DateTime? TargetEarliestTelemetryDateTime {get; private set;}

    // private int _numberOfHoursToDisplay;

    //     get {
    //         return _numberOfHoursToDisplay;
    //     }
    // }

    public TelemetryGraphModel(ILogger<TelemetryGraphModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        // NumberOfHoursToDisplay = 24;
        
        StartDate = DateTime.Now.AddHours(-24).ToUniversalTime();
        EndDate = DateTime.Now.ToUniversalTime();

        StorageHelper storageHelper;
        storageHelper = new StorageHelper(configuration, logger, memoryCache);
        SasToken = storageHelper.SasToken;
    }

    // [BindProperty]
    // public int NumberOfHoursToDisplay {get; set; }

    [BindProperty]
    public int WindowInHours {get; set; }

    [BindProperty]
    public bool LatestAvailableData {get; set; } = true;

    // [BindProperty]
    // public int PeriodOffset {get; set; } = 0;

    // [BindProperty]
    // public int PeriodDescription {get; set; }

    public IActionResult OnGet(int id, DateTime? startDate = null, DateTime? endDate = null)
    {
        _logger.LogInformation("TelemetryGraph.OnGet");
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        if(startDate!=null){
            StartDate = startDate.Value.ToUniversalTime();
        }

        if(endDate!=null){
            EndDate = endDate.Value.ToUniversalTime();
        }

        WindowInHours = (int)EndDate.Subtract(StartDate).TotalHours;// (int)(EndDate - StartDate) / 1000 / 60 / 60;

        // If we're within 5 minutes of now, disable Next button.
        if(DateTime.Now.ToUniversalTime().Subtract(EndDate).TotalMinutes > 5){
            LatestAvailableData = false;
        }


        // PeriodDescription = "Displaying " + FromDate.ToString("yyyy-MM-dd HH:mm") + " to " + ToDate.ToString("yyyy-MM-dd HH:mm");

        // if(numberOfHoursToDisplay!=null){
        //     NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        // }

        _logger.LogInformation($"About to query latest telemetry.... {stopwatch.ElapsedMilliseconds}ms");

        if(_appDbContext.Telemetry.Any(t => t.DeviceId == id) == false){
            return RedirectToPage("/NotFound");
        }

        // var LatestTelemetryDateTime = _appDbContext.Telemetry
        //     .Where(t => t.DeviceId == id)
        //     .OrderByDescending(t => t.Timestamp)
        //     .Select(t => t.Timestamp)
        //     .FirstOrDefault();

        // if(LatestTelemetryDateTime==null || !LatestTelemetryDateTime.HasValue || LatestTelemetryDateTime.Value == DateTime.MinValue){
        //     return RedirectToPage("/NotFound");
        // }

        // DateTime cutOffStart = LatestTelemetryDateTime.Value.AddHours(-1 * NumberOfHoursToDisplay);
        // DateTime cutOffEnd = LatestTelemetryDateTime.Value;

        // if(periodOffset!=null){
        //     PeriodOffset = periodOffset.Value;
        //     cutOffStart = cutOffStart.AddHours(-1 * NumberOfHoursToDisplay * PeriodOffset);
        //     cutOffEnd = cutOffEnd.AddHours(-1 * NumberOfHoursToDisplay * PeriodOffset);
        // }

        _logger.LogInformation($"About to get device info, including telemetry and latest image.... {stopwatch.ElapsedMilliseconds}ms");

        var d = _appDbContext.Devices
            .Include(d => d.Telemetries.Where(t => t.Timestamp >= StartDate && t.Timestamp <= EndDate && t.DeviceId == id))
            // .Include(d => d.Images.Where(i => i.Timestamp >= StartDate && i.Timestamp <= EndDate && i.DeviceId == id).OrderByDescending(i => i.Timestamp).Take(1))
            .Include(d => d.Images.Where(i => i.Timestamp >= StartDate && i.DeviceId == id).OrderByDescending(i => i.Timestamp).Take(1))
            .AsSplitQuery()
            .FirstOrDefault(d => d.Id == id);

        _logger.LogInformation($"Retrieved get device info. {stopwatch.ElapsedMilliseconds}ms");

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        // if(d.Telemetries.Count()==0){
        //     return RedirectToPage("/NotFound");
        // }

        _logger.LogInformation($"About to get latest telemetry data... {stopwatch.ElapsedMilliseconds}ms");

        // EarliestTelemetryDateTime = d.Telemetries
        //     .OrderBy(t => t.Timestamp)
        //     .Select(t => t.Timestamp)
        //     .FirstOrDefault();

        // EarliestTelemetryDateTime = d.Telemetries.Min(t => t.Timestamp);
        // LatestTelemetryDateTime = d.Telemetries.Max(t => t.Timestamp);
        // TargetLatestTelemetryDateTime = LatestTelemetryDateTime;
        // // TargetEarliestTelemetryDateTime = cutOffStart;
        // TargetEarliestTelemetryDateTime = LatestTelemetryDateTime.Value.AddHours(-1 * NumberOfHoursToDisplay);
        

        _logger.LogInformation($"Retrieved latest telemetry data. {stopwatch.ElapsedMilliseconds}ms");

        return Page();

    }
}