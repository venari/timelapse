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
public class ImageHistoryModel : PageModel
{
    private readonly ILogger<ImageHistoryModel> _logger;
    private AppDbContext _appDbContext;

    public List<Device> Devices {get; private set;}
    public string SasToken {get; private set;}

    public ImageHistoryModel(ILogger<ImageHistoryModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
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
    public int DeviceId {get; set; }

    [BindProperty]
    public DateTime StartDate {get; set;}

    [BindProperty]
    public DateTime EndDate {get; set;}

    public List<DateTime> DateRange {get; set;}

    public List<Image> Images {get; set;} = new List<Image>();

    // public class PerformanceDetail{
    //     public DateTime Timestamp {get; set;}
    //     public Image FirstImage {get; set;}
    //     public int TotalImages {get; set;}
    //     public int MaxPendingImages {get; set;}
    // }

    // public class PerformanceSummary{
    //     public int DeviceId {get; set;}
    //     public string DeviceName {get; set;}
    //     public string DeviceDescription {get; set;}
    //     public List<PerformanceDetail> PerformanceDetails {get; set;} = new List<PerformanceDetail>();
    // }

    // public List<PerformanceSummary> PerformanceSummaries {get; set;} = new List<PerformanceSummary>();        

    public IActionResult OnGet(int id, int? numberOfHoursToDisplay = null)
    {
        DeviceId = id;
        
        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        EndDate = DateTime.UtcNow;
        StartDate = EndDate.AddHours(-1 * NumberOfHoursToDisplay);
        var StartDateOnTheHour = StartDate.RoundDownToNearestHour(); //.AddMinutes(-1 * StartDate.Minute).AddSeconds(-1 * StartDate.Second).AddMilliseconds(-1 * StartDate.Millisecond);
        DateRange = Enumerable.Range(0, 1 + NumberOfHoursToDisplay)
            .Select(offset => 
                StartDateOnTheHour.AddHours(offset)
            )
            .ToList();
        // DateRange = Enumerable.Range(0, 1 + (int)EndDate.Subtract(StartDate).TotalHours).Select(offset => 
        //     StartDate.AddHours(offset)
        // ).ToList();

        if(_appDbContext.Images.Any(t => t.Timestamp >= StartDate && t.Timestamp <= EndDate) == false){
            return RedirectToPage("/NotFound");
        }
    
        var device = _appDbContext.Devices
            .Include(d => d.Images.Where(i => i.Timestamp >= StartDate && i.Timestamp <= EndDate).OrderBy(i => i.Timestamp))
            // .Include(d => d.Images.GroupBy(i => i.Timestamp.RoundDownToNearestHour()).Select(g => new {Timestamp = g.Key, FirstImage = g.OrderBy(i => i.Timestamp).FirstOrDefault()}))
            // .Include(d => d.Telemetries.Where(t => t.Timestamp >= StartDate && t.Timestamp <= EndDate).OrderBy(t => t.Timestamp))
            // .AsSplitQuery()
            .First(d => d.Id == id);

        Images = device.Images
                .GroupBy(i => i.Timestamp.RoundDownToNearestHour())
                // .Select(g => new {Timestamp = g.Key, FirstImage = g.OrderBy(i => i.Timestamp).FirstOrDefault()})
                .Select(g => g.OrderBy(i => i.Timestamp).First())
                .ToList();

        return Page();

    }
}