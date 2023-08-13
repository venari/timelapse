using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;

namespace timelapse.api.Pages.Events;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public List<Device> devices {get;}
    public IEnumerable<Image> images {get; set;}
    public string SasToken {get; private set;}
    public List<Areas.Identity.Data.AppUser> Users {get; private set;}

    public IndexModel(ILogger<IndexModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;

        DateTime cutOff = DateTime.UtcNow.AddDays(-2);


        devices = _appDbContext.Devices
            // .Include(d => d.Telemetries.Where(t => t.Timestamp >= cutOff))
            // .Include(d => d.Images.OrderByDescending(i => i.Timestamp).Take(1))

            .Include(d => d.Events)
            .ThenInclude(e => e.EventType)

            .Include(d => d.Events)
            .ThenInclude(e => e.StartImage)

            .Include(d => d.Events)
            .ThenInclude(e => e.EndImage)
            // .AsSplitQuery()
            // .OrderBy(d => d.Name)
            .OrderBy(d => d.Description)
            // .Where(d => d.Retired == false)
            .ToList();

        images = _appDbContext.Images;
        _storageHelper = new StorageHelper(configuration, logger, memoryCache);

        var sasUri = _storageHelper.GenerateSasUri();
        // Extract the Token from the URI
        SasToken = sasUri.Query;

        // _appDbContext.Database.EnsureCreated();
    }

    // public Uri EventStartImageUri(Event Event){
    //     var image = images.Where(i => i.DeviceId == Event.DeviceId && i.Timestamp >= Event.StartTime).OrderBy(i => i.Timestamp).FirstOrDefault();

    //     if(image!=null){
    //         return image.BlobUri;
    //     }

    //     return null;
    // }

    // public Uri EventEndImageUri(Event Event){
    //     var image = images.Where(i => i.DeviceId == Event.DeviceId && i.Timestamp <= Event.EndTime).OrderByDescending(i => i.Timestamp).FirstOrDefault();

    //     if(image!=null){
    //         return image.BlobUri;
    //     }

    //     return null;
    // }

    public void OnGet()
    {
        Users = _appDbContext.Users.ToList();

    }
}