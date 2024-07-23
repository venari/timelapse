// See https://aka.ms/new-console-template for more information
using System.Security.Cryptography;
using extract.imagery.Helpers;
using extract.imagery.infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using timelapse.core.models;


internal class Program
{
    public static List<CameraDescriptionOverride> cameraDescriptionOverrides = new List<CameraDescriptionOverride>(){

// Initial installs

    new CameraDescriptionOverride(){deviceName = "envirocam-a", startTime = null, endTime = new DateTime(2024, 02, 01), descriptionOverride = "Flat Bush - Site 10 - Flat Bush Outflow"},
    new CameraDescriptionOverride(){deviceName = "envirocam-a", startTime = new DateTime(2024, 02, 01), endTime = new DateTime(2024, 05, 21), descriptionOverride = "Owera - Site 20 - Corner of Tauhere Road and Kaupeka Road"},
    // Wiri - Site 08 - 55 Ash Road, near Griffins factory 

    // new CameraDescriptionOverride(){deviceName = "envirocam-k", startTime = new DateTime(2024, 05, 21), endTime = null, descriptionOverride = "Owera - Site 20 - Corner of Tauhere Road and Kaupeka Road"},
    
    
    new CameraDescriptionOverride(){deviceName = "envirocam-b", startTime = null, endTime = new DateTime(2024, 01, 24), descriptionOverride = "Massey, Roundabout Neretva Ave and Biokovo Street"},
    new CameraDescriptionOverride(){deviceName = "envirocam-b", startTime = new DateTime(2024, 02, 21), endTime = new DateTime(2024, 05, 02), descriptionOverride = "Milldale - Site 21 - Milldale Drive looking towards Hicks Road and Waiwai Drive"},

    new CameraDescriptionOverride(){deviceName = "sediment-pi-zero-w-v1-c", startTime = null, endTime = new DateTime(), descriptionOverride = "Wiri stream (by Griffins)"},

    new CameraDescriptionOverride(){deviceName = "envirocam-d", startTime = null, endTime = new DateTime(), descriptionOverride = "Wairau Valley, 17 Silverfield"},

    new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = null, endTime = new DateTime(2023, 11, 23), descriptionOverride = "Flat Bush - Site 5 - Bremner Ridge St & Alluvial St"},
    new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = new DateTime(2023, 12, 14), endTime = new DateTime(2024, 02, 06), descriptionOverride = "Wiri - Site 08 - 39 Ash Road, stream by Griffins"},
    new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = new DateTime(2024, 02, 06), endTime = new DateTime(2024, 04, 11), descriptionOverride = "Wiri - Site 08 - 55 Ash Road, near Griffins factory"},
    // new CameraDescriptionOverride(){deviceName = "envirocam-e", startTime = new DateTime(2024, 05, 21), endTime = null, descriptionOverride = "Site 25"},
    
    new CameraDescriptionOverride(){deviceName = "envirocam-f", startTime = null, endTime = new DateTime(2024, 02, 28), descriptionOverride = "Massey, corner Pūwhā Street and Bikovo Street"},
    new CameraDescriptionOverride(){deviceName = "envirocam-f", startTime = new DateTime(2024, 02, 28), endTime = new DateTime(2024, 05, 21), descriptionOverride = "Flat Bush - Site 16 - Southridge Road"},
    // new CameraDescriptionOverride(){deviceName = "envirocam-f", startTime = new DateTime(2024, 05, 21), endTime = null, descriptionOverride = "Wairau Valley Site 17"},


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

    new CameraDescriptionOverride(){deviceName = "envirocam-2w-b", startTime = null, endTime = new DateTime(2024, 1, 25), descriptionOverride = "Swanson - Site 11 - Duncan Drive near Saw Lane"},
    // new CamaraDescriptionOverride(){deviceName = "sediment-pi-zero-2w-b", startTime = new DateOnly(2024, 02, 01), endTime = null, descriptionOverride = "Rockpool"},

    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Flat Bush - Site 22 - Southridge Road"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Massey - Site 11 - Pūwhā Street and Bikovo Street"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Massey - Site 9 - Roundabout Neretva Ave and Biokovo Street"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Swanson - Site 21 - Kiokio Place and Kātote Avenue"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Swanson - Site 11 - Duncan Drive near Saw Lane"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Wairau Valley - Site 3 - 17 Silverfield"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Wiri - Site 8 - Wiri stream (by Griffins)"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Long Bay - Site 1 - Tupa Street towards Kumukumu Road"},
    // new CameraDescriptionOverride(){deviceName = "", startTime = null, endTime = null, descriptionOverride = "Long Bay - Site 2 - Glenvar Ridge Road catchment"},
};

// Site	Location	deviceId	Telemetry Link	Link with API Key	Location	Orientation	Pi hostname	Installed
// NEW
// 	Swanson, Kate Duncan Drive near Saw Lane	11	https://timelapse-dev.azurewebsites.net/TelemetryGraph/11	https://timelapse-dev.azurewebsites.net/ImageView/11?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.855351498832206, 174.59274919783957	280	sediment-pi-zero-2w-b	6 June
// NEW	Flat Bush outflow	10	https://timelapse-dev.azurewebsites.net/TelemetryGraph/10	https://timelapse-dev.azurewebsites.net/ImageView/10?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.98327400859851, 174.94274415469874	90	sediment-pi-zero-w-v1-a	7 June
// Site 9	Massey, Roundabout Neretva Ave and Biokovo Street	13	https://timelapse-dev.azurewebsites.net/TelemetryGraph/13	https://timelapse-dev.azurewebsites.net/ImageView/13?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.83163625238125, 174.59318081366217	170	sediment-pi-zero-w-v1-b	17 May
// Site 8	Wiri stream (by Griffins)	14	https://timelapse-dev.azurewebsites.net/TelemetryGraph/14	https://timelapse-dev.azurewebsites.net/ImageView/14?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-37.00543002899484, 174.86688187669355	350	sediment-pi-zero-w-v1-c	6 June
// Site 3	Wairau Valley, 17 Silverfield	15	https://timelapse-dev.azurewebsites.net/TelemetryGraph/15	https://timelapse-dev.azurewebsites.net/ImageView/15?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.782385691677405, 174.74390222390488	315	sediment-pi-zero-w-v1-d	15 May
// Site 5
// 	Flat Bush, corner Bremner Ridge St & Alluvial St 	25	https://timelapse-dev.azurewebsites.net/TelemetryGraph/25	https://timelapse-dev.azurewebsites.net/ImageView/25?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.98281329382578, 174.94062268850828	180	sediment-pi-zero-w-v1-e	18 May
// Site 11	Massey, corner Pūwhā Street and Bikovo Street 	19	https://timelapse-dev.azurewebsites.net/TelemetryGraph/19	https://timelapse-dev.azurewebsites.net/ImageView/19?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.83100245872986, 174.59269901880853	180	sediment-pi-zero-w-v1-f	17 May
// Site 2	Long Bay, Glenvar Ridge Road catchment	20	https://timelapse-dev.azurewebsites.net/TelemetryGraph/20	https://timelapse-dev.azurewebsites.net/ImageView/20?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.687106658964574, 174.7338652381412	340	sediment-pi-zero-w-v1-g	17 May
// NEW	Southridge Road, Flat Bush	22	https://timelapse-dev.azurewebsites.net/TelemetryGraph/22	https://timelapse-dev.azurewebsites.net/ImageView/22?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.98376538245302, 174.93945084344014	315	sediment-pi-zero-w-v1-h	7 June
// NEW	Swanson, corner Kiokio Place and Kātote Avenue	21	https://timelapse-dev.azurewebsites.net/TelemetryGraph/21	https://timelapse-dev.azurewebsites.net/ImageView/21?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.86794920966966, 174.5727178796489	135	sediment-pi-zero-w-v1-i	6 June
// Site 7
// 	Flat Bush Greenstead Close	24	https://timelapse-dev.azurewebsites.net/TelemetryGraph/24	https://timelapse-dev.azurewebsites.net/ImageView/24?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.98077265999506, 174.93885132305533	170	sediment-pi-zero-w-v1-j	18 May
// Site 1	Long Bay, Tupa Street towards Kumukumu Road 	23	https://timelapse-dev.azurewebsites.net/TelemetryGraph/23	https://timelapse-dev.azurewebsites.net/ImageView/23?api-key={3d9e8644-4507-489e-ae14-17bd6d968ae9}	-36.679817900807215, 174.73862170607092	350	sediment-pi-zero-w-v1-k	17 May

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
                writer.WriteLine("\"Event Description\",\"Device Name\",\"Device Description\",\"Event Type\",\"Start Time\",\"End Time\",\"Event Detail Page\"");

                foreach (var Event in events.OrderBy(e => e.Device.Name).ThenBy(e => e.StartTime))
                {
                    EventInfo eventInfo = new EventInfo(Event);
                    // Convert event types into csv list of event types

                    writer.WriteLine(eventInfo.csvLine);
                }
            }

            foreach (var Event in events)
            {
                EventInfo eventInfo = new EventInfo(Event);
                logger.LogInformation($"Event Description: {Event.Description}, Device Name:{Event.Device.Name}, {Event.Device.Description}.");
                logger.LogInformation($"Event Start Time: {Event.StartTime.ToLocalTime()}, End Time: {Event.EndTime.ToLocalTime()}");
                logger.LogInformation($"CSV: {eventInfo.csvLine}");
                logger.LogInformation($"EventFolder: {eventInfo.EventFolder}");

                var EventImages = appDbContext.Images
                    .Where(i => i.DeviceId == Event.DeviceId && i.Timestamp >= Event.StartTime.ToUniversalTime() && i.Timestamp <= Event.EndTime.ToUniversalTime())
                    .OrderBy(i => i.Timestamp)
                    .ToList();

                logger.LogInformation($"Number of images: {EventImages.Count()}");

                // Create a folder for the event
                var eventFolder = Path.Combine(outputFolder, eventInfo.EventFolder);

                if (!Directory.Exists(eventFolder))
                {
                    Directory.CreateDirectory(eventFolder);
                }

                // Download first image...

                StorageHelper helper = new StorageHelper(configuration, logger, null);

                if (EventImages.Count() > 0)
                {
                    var firstImage = EventImages.First();
                    var firstImageFilePath = Path.Combine(eventFolder, $"{firstImage.Timestamp.ToString("yyyy-MM-ddTHH-mm-ss")}.jpg");

                    if (!File.Exists(firstImageFilePath))
                    {
                        var blobName = firstImage.BlobUri.Segments.Last();

                        helper.Download(blobName, firstImageFilePath);
                        // using (var webClient = new System.Net.WebClient())
                        // {
                        //     webClient.DownloadFile(firstImage.BlobUri, firstImageFilePath);
                        // }
                    }
                }
            }
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
        this.EventTypesCSV = string.Join(",", Event.EventTypes.Select(et => et.Name));
        this.EventDetailPage = $"https://timelapse-dev.azurewebsites.net/Events/Detail/{Event.Id}";

        string eventDeviceDescription = Event.Device.Description;

        // Check if there is a camera description override
        var cameraDescriptionOverride = Program.cameraDescriptionOverrides.FirstOrDefault(cdo => cdo.deviceName == Event.Device.Name && (cdo.startTime == null || cdo.startTime <= Event.StartTime) && (cdo.endTime == null || cdo.endTime >= Event.EndTime));

        if(cameraDescriptionOverride != null)
        {
            eventDeviceDescription = cameraDescriptionOverride.descriptionOverride;
        }
        
        this.EventFolder = $"Event {Event.Id} - {Event.Device.Name} - {Event.Device.Description}";
        // this.FirstImageFilePath = Event.FirstImageFilePath;
    }
    public string DeviceDescription {get; set;}
    public string EventTypesCSV {get; set;}
    public string EventDetailPage {get; set;}
    public string EventFolder {get; set;}
    public string FirstImageFilePath {get; set;}

    public string csvLine {
        get {
            var line = $"\"{Description}\",\"{Device.Name}\",\"{DeviceDescription}\",\"{EventTypesCSV}\",{StartTime.ToLocalTime()},{EndTime.ToLocalTime()},{EventDetailPage}";
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
