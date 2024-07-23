// See https://aka.ms/new-console-template for more information
using extract.imagery.infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using timelapse.core.models;


// Define logger
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole();
});

// Define configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();


ILogger logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation(configuration.GetConnectionString("DefaultConnection"));

using (var appDbContext = new AppDbContext(configuration))
{
    // var devices = dbContext.Devices.ToList();
    var events = appDbContext.Events
        .Include(e => e.EventTypes)
        .Include(e => e.Device)
        .ToList();
        
    var eventsCount = events.Count();
    logger.LogInformation($"Events count: {eventsCount}");

    foreach(var Event in events)
    {
        logger.LogInformation($"Event Description: {Event.Description}, Device Name:{Event.Device.Name}, {Event.Device.Description}.");

        foreach(var eventType in Event.EventTypes)
        {
            logger.LogInformation($"Event Type: {eventType.Name}");
        }

        logger.LogInformation($"Event Start Time: {Event.StartTime.ToLocalTime()}, End Time: {Event.EndTime.ToLocalTime()}");

        var EventImages = appDbContext.Images
            .Where(i => i.DeviceId == Event.DeviceId && i.Timestamp >= Event.StartTime.ToUniversalTime() && i.Timestamp <= Event.EndTime.ToUniversalTime())
            .OrderBy(i => i.Timestamp)
            .Select(i => new ImageSubset{
                Id = i.Id,
                Timestamp = i.Timestamp,
                BlobUri = i.BlobUri
            })
            .ToList();

        logger.LogInformation($"Number of images: {EventImages.Count()}");
    }
}