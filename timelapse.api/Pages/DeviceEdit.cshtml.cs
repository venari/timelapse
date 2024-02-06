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

        // public List<Device> devices {get;}

        public string BasemapURL {get;}

        // [BindProperty]
        // public DeviceLocation CurrentLocation {get; set;}

        [BindProperty]
        public bool LocationMoved {get; set;}

        [BindProperty]
        public string LocationDescription {get; set;}

        [BindProperty]
        public double? Longitude {get; set;}

        [BindProperty]
        public double? Latitude {get; set;}

        [BindProperty]
        public int? Heading {get; set;}

        [BindProperty]
        public int? Pitch {get; set;}

        [BindProperty]
        public int? HeightMM {get; set;}

        public DeviceEditModel(ILogger<DeviceEditModel> logger, AppDbContext appDbContext, IConfiguration configuration)
        {
            _logger = logger;
            _appDbContext = appDbContext;
            // devices = _appDbContext.Devices
            //     // .Include(d => d.Telemetries)
            //     // .Include(d => d.Images)
            //     // .AsSplitQuery()
            //     .ToList();

            BasemapURL = configuration["LINZ-Aerial-Imagery-Basemap-XYZ-Template"];
            string basemapAPIKey = configuration["LINZApiKey"];
            BasemapURL = BasemapURL.Replace("<LINZ-api-key>", basemapAPIKey);
        }
 
        public IActionResult OnGet(int id)
        {
            Device = _appDbContext.Devices
                .Include(d => d.DeviceLocations)
                .FirstOrDefault(d => d.Id == id);
            
            if(Device.CurrentLocation != null){
                Longitude = Device.CurrentLocation.Longitude;
                Latitude = Device.CurrentLocation.Latitude;
                Heading = Device.CurrentLocation.Heading;
                Pitch = Device.CurrentLocation.Pitch;
                HeightMM = Device.CurrentLocation.HeightMM;
                LocationDescription = Device.CurrentLocation.Description;
            }

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

            var device = _appDbContext.Devices
            .Include(d => d.DeviceLocations)
            .FirstOrDefault(d => d.Id == id);

            device.Name = Device.Name;
            device.Description = Device.Description;
            device.SupportMode = Device.SupportMode;
            device.MonitoringMode = Device.MonitoringMode;
            device.HibernateMode = Device.HibernateMode;
            device.Service = Device.Service;
            device.WideAngle = Device.WideAngle;
            device.Retired = Device.Retired;

            if(Latitude.HasValue && Longitude.HasValue){

                var deviceLocation = device.CurrentLocation;

                if(deviceLocation == null || LocationMoved){
                    deviceLocation = new DeviceLocation();
                    device.DeviceLocations.Add(deviceLocation);
                }

                deviceLocation.Latitude = Latitude.Value;
                deviceLocation.Longitude = Longitude.Value;
                deviceLocation.Heading = Heading;
                deviceLocation.Pitch = Pitch;
                deviceLocation.HeightMM = HeightMM;
                deviceLocation.Timestamp = DateTime.UtcNow;
                deviceLocation.Description = LocationDescription;
            }

            _appDbContext.Devices.Update(device);
            // _appDbContext.Devices.Update(device.CurrentLocation);
            await _appDbContext.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
//#pragma warning restore CS8618
//#pragma warning restore CS8602
}
