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
public class MapModel : PageModel
{
    private readonly ILogger<MapModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public List<Device> devices {get; private set;}
    public string SasToken {get; private set;}

    public string BasemapURL {get;}

    public MapModel(ILogger<MapModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        _storageHelper = new StorageHelper(configuration, logger, memoryCache);

        BasemapURL = configuration["LINZ-Aerial-Imagery-Basemap-XYZ-Template"];
        string basemapAPIKey = configuration["LINZApiKey"];
        BasemapURL = BasemapURL.Replace("<LINZ-api-key>", basemapAPIKey);

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
            .Include(d => d.DeviceLocations.OrderByDescending(l => l.Timestamp).Take(1))
            .AsSplitQuery()
            // .OrderBy(d => d.Name)
            .OrderBy(d => d.Description)
            .Where(d => d.Retired == false)
            .ToList();

        _logger.LogInformation($"Query done {stopwatch.ElapsedMilliseconds}ms");

        var sasUri = _storageHelper.GenerateSasUri();
        // Extract the Token from the URI
        SasToken = sasUri.Query;

        // _appDbContext.Database.EnsureCreated();
    }
}