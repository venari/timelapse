using Microsoft.EntityFrameworkCore;
using timelapse.core.models;
using timelapse.functions.infrastructure;

namespace timelapse.functions.services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Event> GetEventAsync(int eventId)
    {
        return await _context.Events
            .Include(e => e.Device)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new ArgumentException($"Event {eventId} not found");
    }

    public async Task<Event> GetEventWithImagesAsync(int eventId)
    {
        return await _context.Events
            .Include(e => e.Device)
            .Include(e => e.StartImage)
            .Include(e => e.EndImage)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new ArgumentException($"Event {eventId} not found");
    }

    public async Task<Device> GetDeviceAsync(int deviceId)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId)
            ?? throw new ArgumentException($"Device {deviceId} not found");
    }

    public async Task UpdateTimelapseAsync(int eventId, string timelapseUrl, string status)
    {
        var eventEntity = await _context.Events.FindAsync(eventId);
        if (eventEntity != null)
        {
            eventEntity.TimelapseUrl = timelapseUrl;
            eventEntity.TimelapseStatus = status;
            eventEntity.TimelapseCreatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateTimelapseStatusAsync(int eventId, string status)
    {
        var eventEntity = await _context.Events.FindAsync(eventId);
        if (eventEntity != null)
        {
            eventEntity.TimelapseStatus = status;
            await _context.SaveChangesAsync();
        }
    }
}