using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
// using timelapse.functions.Models;
using timelapse.core.models;
using timelapse.functions.services;

namespace timelapse.functions.functions;

public class CreateTimelapseOutput
{
    [ServiceBusOutput("timelapse-queue", Connection = "ServiceBusConnection")]
    public ServiceBusMessage? QueueMessage { get; set; }
    public HttpResponseData HttpResponse { get; set; } = null!;
}

public class TimelapseFunction
{
    private readonly ILogger<TimelapseFunction> _logger;
    private readonly IEventService _eventService;
    private readonly IImageService _imageService;
    private readonly IBlobStorageService _blobService;
    private readonly VideoProcessingService _videoService;

    public TimelapseFunction(
        ILogger<TimelapseFunction> logger,
        IEventService eventService,
        IImageService imageService,
        IBlobStorageService blobService,
        VideoProcessingService videoService)
    {
        _logger = logger;
        _eventService = eventService;
        _imageService = imageService;
        _blobService = blobService;
        _videoService = videoService;
    }

    [Function("HelloWorld")]
    public async Task<string> HelloWorld(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        return "Hello, World!";
    }

    [Function("CreateTimelapse")]
    public async Task<CreateTimelapseOutput> CreateTimelapse(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        Console.WriteLine("=== FUNCTION: CreateTimelapse function called ===");
        _logger.LogWarning("=== CreateTimelapse function called ===");
        
        var hasContentType = req.Headers.TryGetValues("Content-Type", out var contentType);
        var hasContentLength = req.Headers.TryGetValues("Content-Length", out var contentLength);
        
        Console.WriteLine($"Content-Type: {(hasContentType ? string.Join(", ", contentType) : "None")}");
        Console.WriteLine($"Content-Length: {(hasContentLength ? string.Join(", ", contentLength) : "Unknown")}");
        Console.WriteLine($"Body CanRead: {req.Body.CanRead}, CanSeek: {req.Body.CanSeek}");
        
        _logger.LogWarning("Content-Type: {ContentType}", hasContentType ? string.Join(", ", contentType) : "None");
        _logger.LogWarning("Content-Length: {ContentLength}", hasContentLength ? string.Join(", ", contentLength) : "Unknown");
        _logger.LogWarning("Body CanRead: {CanRead}, CanSeek: {CanSeek}, Position: {Position}, Length: {Length}", 
            req.Body.CanRead, req.Body.CanSeek, req.Body.CanSeek ? req.Body.Position : -1, req.Body.CanSeek ? req.Body.Length : -1);
        
        TimelapseRequest? request;
        try
        {
            // Read the body as string first
            string requestBody;
            using (var reader = new StreamReader(req.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }
            
            Console.WriteLine($"=== FUNCTION: Raw body length: {requestBody?.Length ?? 0} ===");
            Console.WriteLine($"=== FUNCTION: Raw body: {requestBody ?? "(null)"} ===");
            
            _logger.LogWarning("Raw request body length: {Length}", requestBody?.Length ?? 0);
            _logger.LogWarning("Raw request body: {RequestBody}", requestBody ?? "(null)");
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                Console.WriteLine("=== FUNCTION: ERROR - Request body is null or empty! ===");
                _logger.LogError("Request body is null or empty!");
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Request body is required");
                return new CreateTimelapseOutput { HttpResponse = bad };
            }
            
            // Deserialize with case-insensitive options to handle camelCase from API
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            request = JsonSerializer.Deserialize<TimelapseRequest>(requestBody, options);
            
            Console.WriteLine($"=== FUNCTION: Parsed - EventId: {request?.EventId}, RequestedBy: {request?.RequestedByUserId} ===");
            
            // Log the parsed request for debugging
            _logger.LogWarning("Parsed CreateTimelapse request: EventId={EventId}, RequestedBy={RequestedBy}", 
                request?.EventId, request?.RequestedByUserId);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"=== FUNCTION: JSON ERROR: {ex.Message} ===");
            _logger.LogError(ex, "Invalid JSON in request body");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON in request body");
            return new CreateTimelapseOutput { HttpResponse = bad };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== FUNCTION: ERROR: {ex.Message} ===");
            _logger.LogError(ex, "Error reading request body");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync($"Error reading request body: {ex.Message}");
            return new CreateTimelapseOutput { HttpResponse = bad };
        }

        if (request == null || (request.EventId == null && (request.DeviceId == null || request.StartTime == null || request.EndTime == null)))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Either EventId or (DeviceId + StartTime + EndTime) must be provided");
            return new CreateTimelapseOutput { HttpResponse = bad };
        }

        try
        {
            if (request.EventId.HasValue)
            {
                await _eventService.UpdateTimelapseStatusAsync(request.EventId.Value, "Pending");
            }

            // Check if Service Bus is configured
            var serviceBusConnection = Environment.GetEnvironmentVariable("ServiceBusConnection");
            var useServiceBus = !string.IsNullOrEmpty(serviceBusConnection) && 
                               serviceBusConnection != "<YOUR_SERVICE_BUS_CONNECTION_STRING>";

            if (useServiceBus)
            {
                Console.WriteLine("=== FUNCTION: Using Service Bus queue ===");
                _logger.LogInformation("Queuing timelapse creation to Service Bus");
                
                var ok = req.CreateResponse(HttpStatusCode.Accepted);
                await ok.WriteAsJsonAsync(new { message = "Timelapse queued", eventId = request.EventId });
                return new CreateTimelapseOutput
                {
                    HttpResponse = ok,
                    QueueMessage = new ServiceBusMessage(JsonSerializer.Serialize(request))
                };
            }
            else
            {
                Console.WriteLine("=== FUNCTION: Processing synchronously (Service Bus not configured) ===");
                _logger.LogWarning("Service Bus not configured - processing timelapse synchronously");
                
                // Process immediately without queuing
                await ProcessTimelapseRequestAsync(request);
                
                var ok = req.CreateResponse(HttpStatusCode.Accepted);
                await ok.WriteAsJsonAsync(new { message = "Timelapse processing started", eventId = request.EventId });
                return new CreateTimelapseOutput
                {
                    HttpResponse = ok,
                    QueueMessage = null // No queue message when processing synchronously
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== FUNCTION: ERROR in CreateTimelapse: {ex.Message} ===");
            _logger.LogError(ex, "Error processing timelapse creation");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync($"Error: {ex.Message}");
            return new CreateTimelapseOutput { HttpResponse = error };
        }
    }

    [Function("ProcessTimelapseQueue")]
    public async Task ProcessTimelapseQueue(
        [ServiceBusTrigger("timelapse-queue", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var request = JsonSerializer.Deserialize<TimelapseRequest>(message.Body.ToString());
        
        try
        {
            await ProcessTimelapseRequestAsync(request);
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create timelapse from queue");
            
            if (request?.EventId.HasValue == true)
            {
                await _eventService.UpdateTimelapseStatusAsync(request.EventId.Value, "Failed");
            }

            // Let the message go to dead letter queue after retries
            throw;
        }
    }

    private async Task ProcessTimelapseRequestAsync(TimelapseRequest request)
    {
        _logger.LogInformation("Processing timelapse request: EventId={EventId}, DeviceId={DeviceId}", 
            request.EventId, request.DeviceId);

        // Update status to processing
        if (request.EventId.HasValue)
        {
            await _eventService.UpdateTimelapseStatusAsync(request.EventId.Value, "Processing");
        }

        // Get images based on request type
        var images = await GetImagesForRequest(request);
        var deviceInfo = await GetDeviceInfo(request);

        if (!images.Any())
        {
            _logger.LogWarning("No images found for timelapse request");
            if (request.EventId.HasValue)
            {
                await _eventService.UpdateTimelapseStatusAsync(request.EventId.Value, "Failed");
            }
            return;
        }

        // Download images from blob storage
        var imagePaths = await DownloadImages(images);

        // Create timelapse
        var videoPath = await _videoService.CreateTimelapseAsync(
            deviceInfo, 
            imagePaths, 
            request.StartTime ?? DateTime.UtcNow.AddDays(-1),
            request.EndTime ?? DateTime.UtcNow,
            request.Description);

        // Upload to blob storage
        var timelapseUrl = await _blobService.UploadVideoAsync(
            videoPath, 
            $"timelapses/{GenerateTimelapseFileName(request)}.mp4");

        // Update database
        if (request.EventId.HasValue)
        {
            await _eventService.UpdateTimelapseAsync(request.EventId.Value, timelapseUrl, "Completed");
        }

        _logger.LogInformation("Timelapse created successfully: {Url}", timelapseUrl);
    }

    private async Task<List<Image>> GetImagesForRequest(TimelapseRequest request)
    {
        if (request.EventId.HasValue)
        {
            return await _imageService.GetImagesForEventAsync(request.EventId.Value);
        }
        else
        {
            return await _imageService.GetImagesForDeviceAndTimeRangeAsync(
                request.DeviceId.Value, 
                request.StartTime.Value, 
                request.EndTime.Value);
        }
    }

    private async Task<Device> GetDeviceInfo(TimelapseRequest request)
    {
        if (request.EventId.HasValue)
        {
            var eventData = await _eventService.GetEventAsync(request.EventId.Value);
            return eventData.Device;
        }
        else
        {
            return await _eventService.GetDeviceAsync(request.DeviceId.Value);
        }
    }

    private async Task<List<string>> DownloadImages(List<Image> images)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "timelapse", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var imagePaths = new List<string>();
        
        foreach (var image in images.OrderBy(i => i.Timestamp))
        {
//            var localPath = Path.Combine(tempDir, $"{image.Id}.jpg");
//            _logger.LogInformation("Downloading image {ImageId} from blob {BlobUri} to {LocalPath}", 
//                image.Id, image.BlobUri, localPath);
//            _logger.LogInformation("Downloading image {ImageId} from blob AbsolutePath {BlobUri_AbsolutePath} to {LocalPath}", 
//                image.Id, image.BlobUri.AbsolutePath, localPath);
            var blobFilename = Path.GetFileName(image.BlobUri.AbsolutePath);
            var localPath = Path.Combine(tempDir, blobFilename);
            await _blobService.DownloadFileAsync(blobFilename, localPath);
            imagePaths.Add(localPath);
        }

        return imagePaths;
    }

    private string GenerateTimelapseFileName(TimelapseRequest request)
    {
        if (request.EventId.HasValue)
        {
            return $"event_{request.EventId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }
        else
        {
            return $"device_{request.DeviceId}_{request.StartTime:yyyyMMdd}_{request.EndTime:yyyyMMdd}_{DateTime.UtcNow:HHmmss}";
        }
    }
}
