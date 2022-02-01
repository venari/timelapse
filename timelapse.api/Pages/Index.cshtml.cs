using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace timelapse.api.Pages;

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
        devices = _appDbContext.Devices
            .Include(d => d.Telemetries)
            .Include(d => d.Images)
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