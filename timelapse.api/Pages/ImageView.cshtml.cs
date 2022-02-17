using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace timelapse.api.Pages;

public class ImageViewModel : PageModel
{
    private readonly ILogger<ImageViewModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public Device device {get; private set;}
    public string SasToken {get; private set;}

    // public DateTime oldestImageTimestamp {get; private set;}
    // public DateTime newestImageTimestamp {get; private set;}
    public Image[] imagesLast24Hours {get; private set;}

    public ImageViewModel(ILogger<ImageViewModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        _storageHelper = new StorageHelper(configuration, logger, memoryCache);
        SasToken = _storageHelper.SasToken;
    }

    public IActionResult OnGet(int id)
    {
        var d = _appDbContext.Devices
            .Include(d => d.Images)
            .FirstOrDefault(d => d.Id == id);

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        imagesLast24Hours = d.Images.Where(i =>i.Timestamp.Date >= DateTime.UtcNow.AddDays(-1).Date).OrderBy(i => i.Timestamp).ToArray();
        if(imagesLast24Hours.Count()==0){
            return RedirectToPage("/NotFound");
        }

        return Page();

    }
}