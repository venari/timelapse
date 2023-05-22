using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;

namespace timelapse.api.Pages;

[Authorize]
public class ImageViewModel : PageModel
{
    private readonly ILogger<ImageViewModel> _logger;
    private AppDbContext _appDbContext;

    public Device device {get; private set;}
    public string SasToken {get; private set;}

    // public DateTime oldestImageTimestamp {get; private set;}
    // public DateTime newestImageTimestamp {get; private set;}
    public Image[] imagesLast24Hours {get; private set;}

    public ImageViewModel(ILogger<ImageViewModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
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
        if(numberOfHoursToDisplay!=null){
            NumberOfHoursToDisplay = numberOfHoursToDisplay.Value;
        }

        DateTime latestImageDateTime = _appDbContext.Images
            .Where(t => t.DeviceId == id)
            .OrderByDescending(t => t.Timestamp)
            .Select(t => t.Timestamp)
            .FirstOrDefault();

        if(latestImageDateTime==null || latestImageDateTime == DateTime.MinValue){
            return RedirectToPage("/NotFound");
        }

        DateTime cutOff = latestImageDateTime.AddHours(-1 * NumberOfHoursToDisplay);

        var d = _appDbContext.Devices
            .Include(d => d.Images.Where(i =>i.Timestamp >= cutOff))
            .FirstOrDefault(d => d.Id == id);

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        imagesLast24Hours = d.Images.OrderBy(i => i.Timestamp).ToArray();
        if(imagesLast24Hours.Count()==0){
            return RedirectToPage("/NotFound");
        }

        return Page();

    }
}