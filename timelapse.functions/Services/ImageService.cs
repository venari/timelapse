using Microsoft.EntityFrameworkCore;
using timelapse.core.models;
using timelapse.functions.infrastructure;

namespace timelapse.functions.services;

public class ImageService : IImageService
{
    private readonly AppDbContext _context;

    public ImageService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Image>> GetImagesForEventAsync(int eventId)
    {
        var eventEntity = await _context.Events.FindAsync(eventId);
        if (eventEntity == null) return new List<Image>();

        // Get all images for the device within the event time range
        return await _context.Images
            .Where(i => i.DeviceId == eventEntity.DeviceId 
                       && i.Timestamp >= eventEntity.StartTime 
                       && i.Timestamp <= eventEntity.EndTime)
            .OrderBy(i => i.Timestamp)
            .ToListAsync();
    }

    public async Task<List<Image>> GetImagesForDeviceAndTimeRangeAsync(int deviceId, DateTime startTime, DateTime endTime)
    {
        return await _context.Images
            .Where(i => i.DeviceId == deviceId 
                       && i.Timestamp >= startTime 
                       && i.Timestamp <= endTime)
            .OrderBy(i => i.Timestamp)
            .ToListAsync();
    }
}