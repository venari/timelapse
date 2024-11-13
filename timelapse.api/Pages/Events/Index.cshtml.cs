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

    public List<Device> devices {get; private set;}
    public IEnumerable<Image> images {get; set;}
    public string SasToken {get; private set;}
    public List<Areas.Identity.Data.AppUser> Users {get; private set;}

    [BindProperty]
    public int NumberOfDaysToDisplay {get; set; }


    public IndexModel(ILogger<IndexModel> logger, AppDbContext appDbContext, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _appDbContext = appDbContext;

        NumberOfDaysToDisplay = 7;

        _storageHelper = new StorageHelper(configuration, logger, memoryCache);

        var sasUri = _storageHelper.GenerateSasUri();
        // Extract the Token from the URI
        SasToken = sasUri.Query;
    }

    public void OnGet(int? numberOfDaysToDisplay = null)
    {
        Users = _appDbContext.Users.ToList();

        if(numberOfDaysToDisplay!=null){
            NumberOfDaysToDisplay = numberOfDaysToDisplay.Value;
        }

        DateTime cutOff = DateTime.UtcNow.AddDays(-1 * NumberOfDaysToDisplay);

        // Temp workaround to shut off before Oct 2024.
        if(cutOff < new DateTime(2024, 10, 01))
        {
            cutOff = new DateTime(2024, 10, 01);
        }

        devices = _appDbContext.Devices

            .Include(d => d.Events.Where(e => e.EndTime >= cutOff))
            .ThenInclude(e => e.EventTypes)

            .Include(d => d.Events)
            .ThenInclude(e => e.StartImage)

            .Include(d => d.Events)
            .ThenInclude(e => e.EndImage)
            // .AsSplitQuery()
            // .OrderBy(d => d.Name)
            .OrderBy(d => d.Description)
            .Where(d => d.Retired == false)
            .ToList();

        images = _appDbContext.Images;
    }
}