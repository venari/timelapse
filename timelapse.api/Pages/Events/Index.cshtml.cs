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
    public string SasToken {get; private set;}

    public IndexModel(ILogger<IndexModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;

        DateTime cutOff = DateTime.UtcNow.AddDays(-2);


        devices = _appDbContext.Devices
            // .Include(d => d.Telemetries.Where(t => t.Timestamp >= cutOff))
            // .Include(d => d.Images.OrderByDescending(i => i.Timestamp).Take(1))
            .Include(d => d.Events)
            // .AsSplitQuery()
            // .OrderBy(d => d.Name)
            .OrderBy(d => d.Description)
            .Where(d => d.Retired == false)
            .ToList();
        _storageHelper = new StorageHelper(configuration, logger, memoryCache);

        var sasUri = _storageHelper.GenerateSasUri();
        // Extract the Token from the URI
        SasToken = sasUri.Query;

        // _appDbContext.Database.EnsureCreated();
    }

    public void OnGet()
    {

    }
}