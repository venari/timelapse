using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;


namespace timelapse.api.Pages
{
#pragma warning disable CS8618
#pragma warning disable CS8602

    public class DeviceCreateModel : PageModel
    {
        private readonly ILogger<DeviceCreateModel> _logger;
        private AppDbContext _appDbContext;
        private StorageHelper _storageHelper;

        public List<Device> devices {get;}

        public DeviceCreateModel(ILogger<DeviceCreateModel> logger, AppDbContext appDbContext, IConfiguration configuration)
        {
            _logger = logger;
            _appDbContext = appDbContext;
            devices = _appDbContext.Devices
                // .Include(d => d.Telemetries)
                // .Include(d => d.Images)
                // .AsSplitQuery()
                .ToList();
        }
 
        // public IActionResult OnGet()
        // {
        //     Movie = new Movie
        //     {
        //         Genre = "Western",
        //         Price = 3.99M,
        //         ReleaseDate = DateTime.Today,
        //         Title = "Conan"
        //     };
        //     return Page();
        // }

        [BindProperty]
        public Device Device { get; set; }

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _appDbContext.Devices.Add(Device);
            await _appDbContext.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
//#pragma warning restore CS8618
//#pragma warning restore CS8602
}
