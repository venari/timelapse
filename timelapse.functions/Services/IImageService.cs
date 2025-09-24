using timelapse.core.models;

namespace timelapse.functions.services;

public interface IImageService
{
    Task<List<Image>> GetImagesForEventAsync(int eventId);
    Task<List<Image>> GetImagesForDeviceAndTimeRangeAsync(int deviceId, DateTime startTime, DateTime endTime);
}