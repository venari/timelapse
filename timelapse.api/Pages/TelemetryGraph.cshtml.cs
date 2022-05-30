using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;

namespace timelapse.api.Pages;

public class TelemetryGraphModel : PageModel
{
    private readonly ILogger<TelemetryGraphModel> _logger;
    private AppDbContext _appDbContext;

    public Device device {get; private set;}
    public string SasToken {get; private set;}

    public TelemetryGraphModel(ILogger<TelemetryGraphModel> logger, AppDbContext appDbContext, IConfiguration configuration)
    {
        _logger = logger;
        _appDbContext = appDbContext;
    }

    public IActionResult OnGet(int id)
    {
        DateTime cutOff = DateTime.UtcNow.AddDays(-2);
        
        var d = _appDbContext.Devices
            .Include(d => d.Telemetries.Where(t => t.Timestamp >= cutOff))
            .FirstOrDefault(d => d.Id == id);

        if(d==null){
            return RedirectToPage("/NotFound");
        }

        device = d;

        if(d.Telemetries.Count()==0){
            return RedirectToPage("/NotFound");
        }

        return Page();

    }
}