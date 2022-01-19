using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.infrastructure;

namespace timelapse.api.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private AppDbContext _appDbContext;

    public IndexModel(ILogger<IndexModel> logger, AppDbContext appDbContext)
    {
        _logger = logger;
        _appDbContext = appDbContext;

        // _appDbContext.Database.EnsureCreated();
    }

    public void OnGet()
    {

        List<timelapse.core.models.Device> devices = _appDbContext.Devices.ToList();

        foreach (timelapse.core.models.Device device in devices)
        {
            _logger.LogInformation(device.Name);
        }        
    }
}
