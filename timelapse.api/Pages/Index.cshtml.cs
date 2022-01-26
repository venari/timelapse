using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;


namespace timelapse.api.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private AppDbContext _appDbContext;

    public List<Device> devices {get;}

    public IndexModel(ILogger<IndexModel> logger, AppDbContext appDbContext)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        devices = _appDbContext.Devices.Include(d => d.Telemetries).ToList();

        // _appDbContext.Database.EnsureCreated();
    }

    public void OnGet()
    {

    }
}
