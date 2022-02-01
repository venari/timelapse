using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;

namespace timelapse.api.Pages;

public class ImageViewModel : PageModel
{
    private readonly ILogger<ImageViewModel> _logger;
    private AppDbContext _appDbContext;
    private StorageHelper _storageHelper;

    public List<Device> devices {get;}
    public Device device {get; private set;}
    public string SasToken {get; private set;}

    public ImageViewModel(ILogger<ImageViewModel> logger, AppDbContext appDbContext, IConfiguration configuration)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        devices = _appDbContext.Devices
            .Include(d => d.Telemetries)
            .Include(d => d.Images)
            .ToList();
        _storageHelper = new StorageHelper(configuration, logger);

        var sasUri = _storageHelper.GenerateSasUri();
        // Extract the Token from the URI
        SasToken = sasUri.Query;

        // _appDbContext.Database.EnsureCreated();
    }

    public void OnGet(int id)
    {
        device = devices.Find(d => d.Id == id);
    }
}