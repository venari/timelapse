// See https://aka.ms/new-console-template for more information
using System.Security.Cryptography;
using extract.imagery.Helpers;
using extract.imagery.infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using timelapse.core.models;
using Spectre.Console;
using Spectre.Console.Cli;
using Humanizer;

internal class Program
{
    const int minutesContext = 5;
    static ILogger logger;
    static AppDbContext appDbContext;
    static string outputFolder;
    static StorageHelper storageHelper;

    public static List<CameraDescriptionOverride> cameraDescriptionOverrides = new List<CameraDescriptionOverride>(){

        new CameraDescriptionOverride(){deviceName = "envirocam-a", startTime = null, endTime = new DateTime(2024, 02, 01), descriptionOverride = "Flat Bush - Site 10 - Flat Bush Outflow"},
        new CameraDescriptionOverride(){deviceName = "envirocam-a", startTime = new DateTime(2024, 02, 01), endTime = new DateTime(2024, 05, 21), descriptionOverride = "Owera - Site 20 - Corner of Tauhere Road and Kaupeka Road"},
        // Wiri - Site 08 - 55 Ash Road, near Griffins factory 

        // new CameraDescriptionOverride(){deviceName = "envirocam-k", startTime = new DateTime(2024, 05, 21), endTime = null, descriptionOverride = "Owera - Site 20 - Corner of Tauhere Road and Kaupeka Road"},
        
        new CameraDescriptionOverride(){deviceName = "envirocam-b", startTime = null, endTime = new DateTime(2024, 01, 24), descriptionOverride = "Massey, Roundabout Neretva Ave and Biokovo Street"},
        new CameraDescriptionOverride(){deviceName = "envirocam-b", startTime = new DateTime(2024, 02, 21), endTime = new DateTime(2024, 05, 02), descriptionOverride = "Milldale - Site 21 - Milldale Drive looking towards Hicks Road and Waiwai Drive"},

        new CameraDescriptionOverride(){deviceName = "sediment-pi-zero-w-v1-c", startTime = null, endTime = new DateTime(2024, 01, 24), descriptionOverride = "Wiri stream (by Griffins)"},

        new CameraDescriptionOverride(){deviceName = "envirocam-d", startTime = null, endTime = new DateTime(), descriptionOverride = "Wairau Valley, 17 Silverfield"},

        new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = null, endTime = new DateTime(2023, 11, 23), descriptionOverride = "Flat Bush - Site 5 - Bremner Ridge St & Alluvial St"},
        new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = new DateTime(2023, 12, 14), endTime = new DateTime(2024, 02, 06), descriptionOverride = "Wiri - Site 08 - 39 Ash Road, stream by Griffins"},
        new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = new DateTime(2024, 02, 06), endTime = new DateTime(2024, 04, 11), descriptionOverride = "Wiri - Site 08 - 55 Ash Road, near Griffins factory"},
        // new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = new DateTime(2024, 05, 21), endTime = null, descriptionOverride = "Site 25"},
        
        new CameraDescriptionOverride(){deviceName = "envirocam-f", startTime = null, endTime = new DateTime(2024, 02, 28), descriptionOverride = "Massey, corner Pūwhā Street and Bikovo Street"},
        new CameraDescriptionOverride(){deviceName = "envirocam-f", startTime = new DateTime(2024, 02, 28), endTime = new DateTime(2024, 05, 21), descriptionOverride = "Flat Bush - Site 16 - Southridge Road"},
        new CameraDescriptionOverride(){deviceName = "envirocam-f", startTime = new DateTime(2024, 05, 21), endTime = null, descriptionOverride = "Wairau Valley - Site 17 - Boat Fix, corner Ashfield Road and Diana Drive"},

        new CameraDescriptionOverride(){deviceName = "sediment-pi-zero-w-v1-g", startTime = null, endTime = new DateTime(2023, 11, 23), descriptionOverride = "Long Bay - Site 2 - Glenvar Ridge Road"},
        // new CameraDescriptionOverride(){deviceName = "sediment-pi-zero-w-v1-g", startTime = new DateTime(2023, 12, 14), endTime = null, descriptionOverride = "Flat Bush, Site 05 - corner Bremner Ridge St & Alluvial St"},

        new CameraDescriptionOverride(){deviceName = "envirocam-h", startTime = null, endTime = new DateTime(2024,02,28), descriptionOverride = "Flat Bush - Site 22 - Southridge Road"},
        // new CameraDescriptionOverride(){deviceName = "envirocam-h", startTime = new DateTime(2024,03,08), endTime = null, descriptionOverride = "Warkworth - Site 24 - South Site"},

        new CameraDescriptionOverride(){deviceName = "envirocam-i", startTime = null, endTime = new DateTime(2024, 02, 22), descriptionOverride = "Swanson - Site 21 - Kiokio Place and Kātote Avenue"},
        // 8 March 24 Warkworth North Site

        new CameraDescriptionOverride(){deviceName = "sediment-pi-zero-w-v1-j", startTime = null, endTime = new DateTime(2024, 02,28), descriptionOverride = "Flat Bush - Site 7 - Greenstead Close"},
        // 28 Feb 24 Site 1 Long Bay

        new CameraDescriptionOverride(){deviceName = "envirocam-k", startTime = null, endTime = new DateTime(2024, 02, 28), descriptionOverride = "Long Bay - Site 1 - Tupa Street towards Kumukumu Road"},
        // 22 May 24 - Orewa - Site 20 - Corner of Tauhere Road and Kaupeka Road 
        // new CameraDescriptionOverride(){deviceName = "envirocam-k", startTime = new DateTime(2024, 05, 21), endTime = null, descriptionOverride = "Owera - Site 20 - Corner of Tauhere Road and Kaupeka Road"},

        new CameraDescriptionOverride(){deviceName = "envirocam-n", startTime = null, endTime = new DateTime(2024, 05, 21), descriptionOverride = "Wairau Valley - Site 17 - Boat Fix, corner Ashfield Road and Diana Drive"},

        new CameraDescriptionOverride(){deviceName = "envirocam-2w-b", startTime = null, endTime = new DateTime(2024, 1, 25), descriptionOverride = "Swanson - Site 11 - Duncan Drive near Saw Lane"},
    };

    private static void Main(string[] args)
    {
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


        logger = loggerFactory.CreateLogger<Program>();
        // logger.LogInformation(configuration.GetConnectionString("DefaultConnection"));

        storageHelper = new StorageHelper(configuration, logger, null);

        using (appDbContext = new AppDbContext(configuration))
        {
            // var devices = dbContext.Devices.ToList();
            var events = appDbContext.Events
                .Include(e => e.EventTypes)
                .Include(e => e.Device)
                .ToList();

            var eventsCount = events.Count();
            // logger.LogInformation($"Events count: {eventsCount}");

            // create output folder if it doesn't already exist
            outputFolder = configuration["OutputFolder"];
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // if clean switch, empty folder
            if (args.Length > 0 && args[0] == "clean")
            {
                DirectoryInfo di = new DirectoryInfo(outputFolder);

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
            }

            AnsiConsole.Progress()
                .HideCompleted(true)
                .StartAsync(async ctx => 
                {
                    // Define tasks
                    var taskCreateSummaryCSV = ctx.AddTask("[green]Create Summary CSV[/]");
                    var taskDownloadEvents = ctx.AddTask("[green]Download Events[/]");
                    taskDownloadEvents.MaxValue = eventsCount;

                    while(!ctx.IsFinished) 
                    {
                        await CreateSummaryCSV(events);
                        taskCreateSummaryCSV.Increment(100);

                        foreach (var Event in events)
                        {
                            var taskDownloadEvent = ctx.AddTask($"[green]Download Event {Event.Id}[/]");

                            CreateEventInfoFile(Event).Wait();
                            DownloadEventImages(Event, taskDownloadEvent).Wait();

                            // taskDownloadEvent.Increment(100);
                            taskDownloadEvents.Increment(1);
                        }
                    }
                });


            // TO DO
            //- add description file

        }
    }

    private static async Task CreateSummaryCSV(List<Event> events){
        try{
            // Create summary.csv file
            var summaryFilePath = Path.Combine(outputFolder, "summary.csv");
            // Delete the file if it exists
            if (File.Exists(summaryFilePath))
            {
                File.Delete(summaryFilePath);
            }

            // Open file for writing
            using (var writer = new StreamWriter(summaryFilePath))
            {
                writer.WriteLine("\"Event ID\",\"Event Description\",\"Device Name\",\"Device Description\",\"Event Type\",\"Start Time\",\"End Time\",\"Duration (hrs)\",\"Event Detail Page\"");

                foreach (var Event in events)//.OrderBy(e => e.Device.Name).ThenBy(e => e.StartTime))
                {
                    EventInfo eventInfo = new EventInfo(Event);
                    // Convert event types into csv list of event types

                    writer.WriteLine(eventInfo.csvLine);
                }
            }


        } catch(Exception ex){
            logger.LogError(ex, "Error creating summary CSV");
        }
    }

    private static async Task CreateEventInfoFile(Event Event){
        try{
            EventInfo eventInfo = new EventInfo(Event);

            // Create a folder for the event
            var eventFolder = Path.Combine(outputFolder, eventInfo.EventFolder);

            var eventInfoFilePath = Path.Combine(eventFolder, "event-info.txt");

            if (!Directory.Exists(eventFolder))
            {
                Directory.CreateDirectory(eventFolder);
            }

            // Delete the file if it exists
            if (File.Exists(eventInfoFilePath))
            {
                File.Delete(eventInfoFilePath);
            }

            // Open file for writing
            using (var writer = new StreamWriter(eventInfoFilePath))
            {
                writer.WriteLine($"Event Description: {eventInfo.Description}");
                writer.WriteLine($"Device Name: {eventInfo.Device.Name}");
                writer.WriteLine($"Device Description: {eventInfo.DeviceDescription}");
                writer.WriteLine($"Event Types: {eventInfo.EventTypesCSV}");
                writer.WriteLine($"Start Time: {eventInfo.StartTime.ToLocalTime()}");
                writer.WriteLine($"End Time: {eventInfo.EndTime.ToLocalTime()}");
                writer.WriteLine($"End Duration: {(eventInfo.EndTime.ToLocalTime() - eventInfo.StartTime.ToLocalTime()).Humanize()}");
                writer.WriteLine($"Event Detail Page: {eventInfo.EventDetailPage}");
            }
        }
        catch(Exception ex){
            logger.LogError(ex, $"Error creating event info file for event {Event.Id}");
        }
    }

    private static async Task DownloadEventImages(Event Event, ProgressTask progressTask){
        try{
            EventInfo eventInfo = new EventInfo(Event);
            // logger.LogDebug($"Event Description: {eventInfo.Description}, Device Name:{eventInfo.Device.Name}, {eventInfo.DeviceDescription}.");
            // logger.LogDebug($"Event Start Time: {Event.StartTime.ToLocalTime()}, End Time: {Event.EndTime.ToLocalTime()}");
            // logger.LogDebug($"CSV: {eventInfo.csvLine}");
            // logger.LogDebug($"EventFolder: {eventInfo.EventFolder}");

            var EventImages = appDbContext.Images
                .Where(i => i.DeviceId == Event.DeviceId && i.Timestamp >= Event.StartTime.AddMinutes(minutesContext*-1).ToUniversalTime() && i.Timestamp <= Event.EndTime.AddMinutes(minutesContext).ToUniversalTime())
                .OrderBy(i => i.Timestamp)
                .ToList();

            // logger.LogInformation($"Number of images: {EventImages.Count()}");

            progressTask.MaxValue = EventImages.Count();

            // Create a folder for the event
            var eventFolder = Path.Combine(outputFolder, eventInfo.EventFolder);

            if (!Directory.Exists(eventFolder))
            {
                Directory.CreateDirectory(eventFolder);
            }

            if (EventImages.Count() > 0)
            {
                foreach(var image in EventImages){
                    var imageFilePath = Path.Combine(eventFolder, $"{image.Timestamp.ToString("yyyy-MM-ddTHH-mm-ss")}.jpg");

                    if (!File.Exists(imageFilePath))
                    {
                        var blobName = image.BlobUri.Segments.Last();

                        await storageHelper.DownloadAsync(blobName, imageFilePath);
                        // storageHelper.Download(blobName, imageFilePath);
                    }
                    progressTask.Increment(1);
                }
            }
        }
        catch(Exception ex){
            logger.LogError(ex, $"Error downloading images for event {Event.Id}");
        }
    }
}



class EventInfo : Event{
    public EventInfo(Event Event){
        this.Id = Event.Id;
        this.DeviceId = Event.DeviceId;
        this.Description = Event.Description;
        this.StartTime = Event.StartTime;
        this.EndTime = Event.EndTime;
        this.Device = Event.Device;
        this.EventTypesCSV = string.Join(", ", Event.EventTypes.Select(et => et.Name));
        this.EventDetailPage = $"https://timelapse-dev.azurewebsites.net/Events/Detail/{Event.Id}";

        this.DeviceDescription = Event.Device.Description;

        // Check if there is a camera description override
        var cameraDescriptionOverride = Program.cameraDescriptionOverrides.FirstOrDefault(cdo => cdo.deviceName == Event.Device.Name && (cdo.startTime == null || cdo.startTime <= Event.StartTime) && (cdo.endTime == null || cdo.endTime >= Event.EndTime));

        if(cameraDescriptionOverride != null)
        {
            this.DeviceDescription = cameraDescriptionOverride.descriptionOverride;
        }
        
        this.EventFolder = $"Event {Event.Id} - {Event.Device.Name} - {DeviceDescription}";
    }
    public string DeviceDescription {get; set;}
    public string EventTypesCSV {get; set;}
    public string EventDetailPage {get; set;}
    public string EventFolder {get; set;}

    public string csvLine {
        get {
            var line = $"{Id},\"{Description}\",\"{Device.Name}\",\"{DeviceDescription}\",\"{EventTypesCSV}\",{StartTime.ToLocalTime()},{EndTime.ToLocalTime()},{(EndTime-StartTime).TotalHours.ToString("0.00")},{EventDetailPage}";
            line = line.Replace(" ", " "); // replace strange space character.
            return line;
    }
    }
}

class CameraDescriptionOverride{
    public string deviceName {get; set;}
    public DateTime? startTime {get; set;}
    public DateTime? endTime {get; set;}
    public string descriptionOverride {get; set;} 
};
