using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.AspNetCore.Authorization;

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

    public TelemetryGraphModel(ILogger<TelemetryGraphModel> logger, AppDbContext appDbContext, IConfiguration configuration)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        NumberOfHoursToDisplay = 24;
    }

    [BindProperty]
    public int NumberOfHoursToDisplay {get; set; }

    public IActionResult OnGet(int id, int? numberOfHoursToDisplay = null)
    {
        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        LatestTelemetryDateTime = _appDbContext.Telemetry
            .Where(t => t.DeviceId == id)
            .OrderByDescending(t => t.Timestamp)
            .Select(t => t.Timestamp)
            .FirstOrDefault();

        if(LatestTelemetryDateTime==null || !LatestTelemetryDateTime.HasValue || LatestTelemetryDateTime.Value == DateTime.MinValue){
            return RedirectToPage("/NotFound");
        }

        DateTime cutOff = LatestTelemetryDateTime.Value.AddHours(-1 * NumberOfHoursToDisplay);
        
        var d = _appDbContext.Devices
            .Include(d => d.Telemetries.Where(t => t.Timestamp >= cutOff))
            .FirstOrDefault(d => d.Id == id);

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        if(d.Telemetries.Count()==0){
            return RedirectToPage("/NotFound");
        }

        EarliestTelemetryDateTime = d.Telemetries
            .OrderBy(t => t.Timestamp)
            .Select(t => t.Timestamp)
            .FirstOrDefault();

        return Page();

    }
}