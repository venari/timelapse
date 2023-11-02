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
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public List<Device> devices {get; private set;}
    // public string SasToken {get; private set;}

    public IndexModel(ILogger<IndexModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        _storageHelper = new StorageHelper(configuration, appDbContext, logger, memoryCache);
    }

    public string GetSasTokenForImage(int imageId){
        return _storageHelper.GetSasTokenForImage(imageId);
    }

    public void OnGet()
    {
        _logger.LogInformation("Index.OnGet");
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        DateTime cutOff = DateTime.UtcNow.AddDays(-2);

        _logger.LogInformation($"About to query.... {stopwatch.ElapsedMilliseconds}ms");

        devices = _appDbContext.Devices
            // .Include(d => d.Telemetries.Where(t => t.Timestamp >= cutOff))
            .Include(d => d.Telemetries.Where(t => t.Timestamp >= cutOff).OrderByDescending(t => t.Timestamp).Take(1))
            .Include(d => d.Images.Where(i => i.Timestamp >= cutOff).OrderByDescending(i => i.Timestamp).Take(1))
            .AsSplitQuery()
            // .OrderBy(d => d.Name)
            .OrderBy(d => d.Description)
            .Where(d => d.Retired == false)
            .ToList();

        _logger.LogInformation($"Query done {stopwatch.ElapsedMilliseconds}ms");

        // _appDbContext.Database.EnsureCreated();
    }
}