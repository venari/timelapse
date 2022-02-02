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
    public int imageCount {get; private set;}

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

        var images = d.Images.OrderBy(i => i.Timestamp).ToList();
        imageCount = images.Count;

        if(imageCount==0){
            return RedirectToPage("/NotFound");
        }

        return Page();

    }
}