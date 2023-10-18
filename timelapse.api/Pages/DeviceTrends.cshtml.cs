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
    // private StorageHelper _storageHelper;

    public List<Device> devices {get; private set;}
    // public string SasToken {get; private set;}

    public DeviceTrendsModel(ILogger<DeviceTrendsModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        NumberOfHoursToDisplay = 24;
        // StorageHelper storageHelper;
        // _storageHelper = new StorageHelper(configuration, appDbContext, logger, memoryCache);
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

        if(_appDbContext.Telemetry.Any(t => t.Device.Retired == false && t.Timestamp >= StartDate && t.Timestamp <= EndDate) == false){
            return RedirectToPage("/NotFound");
        }
                
        devices = _appDbContext.Devices
            .Where(d => d.Retired == false)
            .ToList();

        if(devices==null || devices.Count()==0){
            return RedirectToPage("/NotFound");
        }

        return Page();

    }
}