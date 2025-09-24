using timelapse.core.models;

namespace timelapse.functions.services;

public interface IEventService
{
    Task<Event> GetEventAsync(int eventId);
    Task<Event> GetEventWithImagesAsync(int eventId);
    Task<Device> GetDeviceAsync(int deviceId);
    Task UpdateTimelapseAsync(int eventId, string timelapseUrl, string status);
    Task UpdateTimelapseStatusAsync(int eventId, string status);
}