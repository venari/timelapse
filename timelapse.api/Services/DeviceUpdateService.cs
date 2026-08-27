using timelapse.core.models;

namespace timelapse.api.Services;

public class DeviceUpdateResult
{
    public bool Success { get; set; }
    public Dictionary<string, string> Errors { get; } = new();
}

// Single source of truth for applying a device settings/camera-config/location edit.
// Used by both the legacy DeviceEdit Razor page and the PUT /api/Devices/{id} endpoint
// so the two can't drift out of sync with each other.
public class DeviceUpdateService
{
    public DeviceUpdateResult ApplyUpdate(Device device, DeviceUpdateRequest request)
    {
        var result = new DeviceUpdateResult();
        var loc = request.Location;

        if (loc != null)
        {
            if (loc.Latitude.HasValue != loc.Longitude.HasValue)
            {
                result.Errors["Location"] = "Latitude and Longitude must both be provided together";
            }
            else if (loc.Latitude.HasValue && string.IsNullOrEmpty(loc.Description))
            {
                result.Errors["Location.Description"] = "Location Description is required";
            }
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        device.Name = request.Name;
        device.Description = request.Description;
        device.ShortDescription = request.ShortDescription;
        device.SupportMode = request.SupportMode;
        device.MonitoringMode = request.MonitoringMode;
        device.HibernateMode = request.HibernateMode;
        device.PowerOff = request.PowerOff;
        device.Service = request.Service;
        device.WideAngle = request.WideAngle;
        device.Retired = request.Retired;
        device.SleepDuringNight = request.SleepDuringNight;
        device.DaytimeStartsAtH = request.DaytimeStartsAtH;
        device.DaytimeEndsAtH = request.DaytimeEndsAtH;
        device.UtcOffsetMinutes = request.UtcOffsetMinutes;
        device.CameraIntervalS = request.CameraIntervalS;
        device.ApiUrl = request.ApiUrl;
        device.Hflip = request.Hflip;
        device.Vflip = request.Vflip;
        device.EnableLongExposureAtNight = request.EnableLongExposureAtNight;
        device.LongExposureXclkHz = request.LongExposureXclkHz;
        device.GeoIntervalS = request.GeoIntervalS;
        device.AutoSyncPeriodS = request.AutoSyncPeriodS;

        if (loc?.Latitude != null && loc.Longitude != null)
        {
            var deviceLocation = device.CurrentLocation;

            if (deviceLocation == null || loc.LocationMoved)
            {
                deviceLocation = new DeviceLocation();
                device.DeviceLocations.Add(deviceLocation);
            }

            deviceLocation.Latitude = loc.Latitude.Value;
            deviceLocation.Longitude = loc.Longitude.Value;
            deviceLocation.Heading = loc.Heading;
            deviceLocation.Pitch = loc.Pitch;
            deviceLocation.HeightMM = loc.HeightMM;
            deviceLocation.Timestamp = DateTime.UtcNow;
            deviceLocation.Description = loc.Description;
        }

        result.Success = true;
        return result;
    }
}
