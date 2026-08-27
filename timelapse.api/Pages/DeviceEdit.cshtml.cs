using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using timelapse.api.Services;
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
        private DeviceUpdateService _deviceUpdateService;

        // public List<Device> devices {get;}

        public string BasemapURL {get;}

        // [BindProperty]
        // public DeviceLocation CurrentLocation {get; set;}

        [BindProperty]
        public bool LocationMoved {get; set;}

        [BindProperty]
        public string? LocationDescription {get; set;}

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

        public DeviceEditModel(ILogger<DeviceEditModel> logger, AppDbContext appDbContext, IConfiguration configuration, DeviceUpdateService deviceUpdateService)
        {
            _logger = logger;
            _appDbContext = appDbContext;
            _deviceUpdateService = deviceUpdateService;
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
            var device = _appDbContext.Devices
            .Include(d => d.DeviceLocations)
            .FirstOrDefault(d => d.Id == id);

            var request = new DeviceUpdateRequest{
                Name = Device.Name,
                Description = Device.Description,
                ShortDescription = Device.ShortDescription,
                SupportMode = Device.SupportMode,
                MonitoringMode = Device.MonitoringMode,
                HibernateMode = Device.HibernateMode,
                PowerOff = Device.PowerOff,
                Service = Device.Service,
                WideAngle = Device.WideAngle,
                Retired = Device.Retired,
                SleepDuringNight = Device.SleepDuringNight,
                DaytimeStartsAtH = Device.DaytimeStartsAtH,
                DaytimeEndsAtH = Device.DaytimeEndsAtH,
                UtcOffsetMinutes = Device.UtcOffsetMinutes,
                CameraIntervalS = Device.CameraIntervalS,
                ApiUrl = Device.ApiUrl,
                Hflip = Device.Hflip,
                Vflip = Device.Vflip,
                EnableLongExposureAtNight = Device.EnableLongExposureAtNight,
                LongExposureXclkHz = Device.LongExposureXclkHz,
                GeoIntervalS = Device.GeoIntervalS,
                AutoSyncPeriodS = Device.AutoSyncPeriodS,
                Location = new DeviceLocationUpdateRequest{
                    LocationMoved = LocationMoved,
                    Description = LocationDescription,
                    Latitude = Latitude,
                    Longitude = Longitude,
                    Heading = Heading,
                    Pitch = Pitch,
                    HeightMM = HeightMM,
                }
            };

            var result = _deviceUpdateService.ApplyUpdate(device, request);

            if(!result.Success){
                foreach(var error in result.Errors){
                    ModelState.AddModelError(error.Key, error.Value);
                }
                return Page();
            }

            await _appDbContext.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
//#pragma warning restore CS8618
//#pragma warning restore CS8602
}
