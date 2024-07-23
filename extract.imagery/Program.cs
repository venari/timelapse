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

    // create output folder if it doesn't already exist
    var outputFolder = configuration["OutputFolder"];
    if (!System.IO.Directory.Exists(outputFolder))
    {
        System.IO.Directory.CreateDirectory(outputFolder);
    }

    // if clean switch, empty folder
    if (args.Length > 0 && args[0] == "clean")
    {
        System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(outputFolder);

        foreach (System.IO.FileInfo file in di.GetFiles())
        {
            file.Delete();
        }
    }

    // Create summary.csv file
    var summaryFilePath = System.IO.Path.Combine(outputFolder, "summary.csv");
    // Delete the file if it exists
    if (System.IO.File.Exists(summaryFilePath))
    {
        System.IO.File.Delete(summaryFilePath);
    }

    // Open file for writing
    using (var writer = new System.IO.StreamWriter(summaryFilePath))
    {
        writer.WriteLine("\"Event Description\",\"Device Name\",\"Device Description\",\"Event Type\",\"Start Time\",\"End Time\",\"Event Detail Page\"");

        foreach(var Event in events.OrderBy(e => e.Device.Name).ThenBy(e => e.StartTime))
        {
            // Convert event types into csv list of event types
            var eventTypes = string.Join(",", Event.EventTypes.Select(et => et.Name));
            var line = $"\"{Event.Description}\",\"{Event.Device.Name}\",\"{Event.Device.Description}\",\"{eventTypes}\",{Event.StartTime.ToLocalTime()},{Event.EndTime.ToLocalTime()},\"https://timelapse-dev.azurewebsites.net/Events/Detail/{Event.Id}\"";
            line = line.Replace(" ", " "); // replace strange space character.
            writer.WriteLine(line);
        }
    }

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