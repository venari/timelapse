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

    public List<DateTime> DateRange {get; set;}

    public class PerformanceDetail{
        public DateTime Timestamp {get; set;}
        public Image FirstImage {get; set;}
        public int TotalImages {get; set;}
        public int MaxPendingImages {get; set;}
    }

    public class PerformanceSummary{
        public int DeviceId {get; set;}
        public string DeviceName {get; set;}
        public string DeviceDescription {get; set;}
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
        var StartDateOnTheHour = StartDate.RoundDownToNearestHour(); //.AddMinutes(-1 * StartDate.Minute).AddSeconds(-1 * StartDate.Second).AddMilliseconds(-1 * StartDate.Millisecond);
        DateRange = Enumerable.Range(0, 1 + NumberOfHoursToDisplay)
            .Select(offset => 
                StartDateOnTheHour.AddHours(offset)
            )
            .ToList();
        // DateRange = Enumerable.Range(0, 1 + (int)EndDate.Subtract(StartDate).TotalHours).Select(offset => 
        //     StartDate.AddHours(offset)
        // ).ToList();

        if(_appDbContext.Telemetry.Any(t => t.Device.Retired == false && t.Timestamp >= StartDate && t.Timestamp <= EndDate) == false){
            return RedirectToPage("/NotFound");
        }
    
        var devices = _appDbContext.Devices
            .Include(d => d.Images.Where(i => i.Timestamp >= StartDate && i.Timestamp <= EndDate).OrderBy(i => i.Timestamp))
            .Include(d => d.Telemetries.Where(t => t.Timestamp >= StartDate && t.Timestamp <= EndDate).OrderBy(t => t.Timestamp))
            .Where(d => d.Retired == false)
            .AsSplitQuery()
            .OrderBy(d => d.ShortDescription);

        foreach(var d in devices){
            var devicePerformanceSummary = new PerformanceSummary{
                DeviceId = d.Id,
                DeviceName = d.Name,
                DeviceDescription = d.ShortDescription,
                PerformanceDetails = d.Images
                    // .GroupBy(i => new {DateUTC = i.Timestamp.Date, HourUTC = i.Timestamp.Hour})
                    // .Select(g => new PerformanceDetail{Timestamp = g.Key.DateUTC.AddHours(g.Key.HourUTC), TotalImages = g.Count()})
                    .GroupBy(i => i.Timestamp.RoundDownToNearestHour())
                    .Select(g => new PerformanceDetail{Timestamp = g.Key, TotalImages = g.Count(), FirstImage = g.OrderBy(i => i.Timestamp).FirstOrDefault()})
                    .ToList()
            };

            var pendingImagesByHour = d.Telemetries
                .GroupBy(t => t.Timestamp.RoundDownToNearestHour())
                .Select(g => new {Timestamp = g.Key, PendingImages = g.Max(t => t.PendingImages)})
                .ToList();

            foreach(var pendingImages in pendingImagesByHour){
                var performanceDetail = devicePerformanceSummary.PerformanceDetails.FirstOrDefault(pd => pd.Timestamp == pendingImages.Timestamp);
                if(performanceDetail!=null){
                    performanceDetail.MaxPendingImages = pendingImages.PendingImages??0;
                }
            }

            PerformanceSummaries.Add(devicePerformanceSummary);

            // var imagesPerDay = d.Images
            //     .GroupBy(i => new {deviceId = i.DeviceId, date = i.Timestamp.Date})
            //     .Select(g => new {Date = g.Key.date, Count = g.Count()}).ToList();
            // // d.ImagesPerDay = imagesPerDay;
        }

        foreach(var ps in PerformanceSummaries){
            if(ps.PerformanceDetails.Count() == 0){
                continue;
            }
            var imagesPerHour = ps.PerformanceDetails;
            var startDate = imagesPerHour.Min(i => i.Timestamp);
            var endDate = imagesPerHour.Max(i => i.Timestamp);
            // var missingTimestamps = DateRange.Except(imagesPerHour.Select(i => i.Timestamp.RoundDownToNearestHour())).ToList();
            var missingTimestamps = DateRange.Except(imagesPerHour.Select(i => i.Timestamp)).ToList();
            foreach(var missingTimestamp in missingTimestamps){
                ps.PerformanceDetails.Add(new PerformanceDetail{Timestamp = missingTimestamp, TotalImages = 0, MaxPendingImages = 0});
            }
            ps.PerformanceDetails = ps.PerformanceDetails.OrderBy(i => i.Timestamp).ToList();
        }



        if(devices==null || devices.Count()==0){
            return RedirectToPage("/NotFound");
        }

        Devices = devices.ToList();

        return Page();

    }
}