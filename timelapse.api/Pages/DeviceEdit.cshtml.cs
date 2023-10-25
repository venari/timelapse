using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using Microsoft.AspNetCore.Authorization;


namespace timelapse.api.Pages
{
#pragma warning disable CS8618
#pragma warning disable CS8602

    [Authorize]
    public class DeviceEditModel : PageModel
    {
        private readonly ILogger<DeviceEditModel> _logger;
        private AppDbContext _appDbContext;
        private StorageHelper _storageHelper;

        public List<Device> devices {get;}

        public DeviceEditModel(ILogger<DeviceEditModel> logger, AppDbContext appDbContext, IConfiguration configuration)
        {
            _logger = logger;
            _appDbContext = appDbContext;
            devices = _appDbContext.Devices
                // .Include(d => d.Telemetries)
                // .Include(d => d.Images)
                // .AsSplitQuery()
                .ToList();
        }
 
        public IActionResult OnGet(int id)
        {
            Device = _appDbContext.Devices.FirstOrDefault(d => d.Id == id);
            if (Device == null)
            {
                return NotFound();
            }
            return Page();
        }

        [BindProperty]
        public Device Device { get; set; }

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var device = _appDbContext.Devices.FirstOrDefault(d => d.Id == id);

            device.Name = Device.Name;
            device.Description = Device.Description;
            device.SupportMode = Device.SupportMode;
            device.MonitoringMode = Device.MonitoringMode;
            device.HibernateMode = Device.HibernateMode;
            device.Retired = Device.Retired;

            _appDbContext.Devices.Update(device);
            await _appDbContext.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
//#pragma warning restore CS8618
//#pragma warning restore CS8602
}
