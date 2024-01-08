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

    public List<Device> Devices {get; private set;}
    public string SasToken {get; private set;}

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

    public class PerformanceDetail{
        public DateTime Date {get; set;}
        public int TotalImages {get; set;}
    }

    public class PerformanceSummary{
        public int DeviceId {get; set;}
        public string DeviceName {get; set;}
        public List<PerformanceDetail> PerformanceDetails {get; set;} = new List<PerformanceDetail>();
    }

    public List<PerformanceSummary> PerformanceSummaries {get; set;} = new List<PerformanceSummary>();

    public IActionResult OnGet(int? numberOfHoursToDisplay = null)
    {
        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        EndDate = DateTime.UtcNow;
        StartDate = EndDate.AddHours(-1 * NumberOfHoursToDisplay);

        // Set to midnight for performance counts below
        StartDate = StartDate.AddHours(-1 * StartDate.Hour);

        if(_appDbContext.Telemetry.Any(t => t.Device.Retired == false && t.Timestamp >= StartDate && t.Timestamp <= EndDate) == false){
            return RedirectToPage("/NotFound");
        }
    

        var devices = _appDbContext.Devices
            // .Include(d => d.Telemetries.Where(t => t.Timestamp >= StartDate && t.Timestamp <= EndDate).OrderBy(t => t.Timestamp))
            .Include(d => d.Images.Where(i => i.Timestamp >= StartDate && i.Timestamp <= EndDate).OrderBy(i => i.Timestamp))
            .Where(d => d.Retired == false)
            .AsSplitQuery();

        foreach(var d in devices){
            PerformanceSummaries.Add(new PerformanceSummary{
                DeviceId = d.Id,
                DeviceName = d.Name,
                PerformanceDetails = d.Images
                    .GroupBy(i => i.Timestamp.Date)
                    .Select(g => new PerformanceDetail{Date = g.Key, TotalImages = g.Count()})
                    .ToList()
            });
            // var imagesPerDay = d.Images
            //     .GroupBy(i => new {deviceId = i.DeviceId, date = i.Timestamp.Date})
            //     .Select(g => new {Date = g.Key.date, Count = g.Count()}).ToList();
            // // d.ImagesPerDay = imagesPerDay;
        }

        foreach(var ps in PerformanceSummaries){
            if(ps.PerformanceDetails.Count() == 0){
                continue;
            }
            var imagesPerDay = ps.PerformanceDetails;
            var startDate = imagesPerDay.Min(i => i.Date);
            var endDate = imagesPerDay.Max(i => i.Date);
            var dateRange = Enumerable.Range(0, 1 + endDate.Subtract(startDate).Days).Select(offset => startDate.AddDays(offset)).ToList();
            var missingDates = dateRange.Except(imagesPerDay.Select(i => i.Date));
            foreach(var missingDate in missingDates){
                ps.PerformanceDetails.Add(new PerformanceDetail{Date = missingDate, TotalImages = 0});
            }
            ps.PerformanceDetails = ps.PerformanceDetails.OrderBy(i => i.Date).ToList();
        }



        if(devices==null || devices.Count()==0){
            return RedirectToPage("/NotFound");
        }

        Devices = devices.ToList();

        return Page();

    }
}