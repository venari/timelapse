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

    [Function("CreateTimelapse")]
    public async Task<CreateTimelapseOutput> CreateTimelapse(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is required");
            return new CreateTimelapseOutput { HttpResponse = bad };
        }

        TimelapseRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<TimelapseRequest>(requestBody);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in request body");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON in request body");
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

            var ok = req.CreateResponse(HttpStatusCode.Accepted);
            await ok.WriteAsJsonAsync(new { message = "Timelapse queued", eventId = request.EventId });
            return new CreateTimelapseOutput
            {
                HttpResponse = ok,
                QueueMessage = new ServiceBusMessage(JsonSerializer.Serialize(request))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing timelapse creation");
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
                await messageActions.CompleteMessageAsync(message);
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
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create timelapse");
            
            if (request.EventId.HasValue)
            {
                await _eventService.UpdateTimelapseStatusAsync(request.EventId.Value, "Failed");
            }

            // Let the message go to dead letter queue after retries
            throw;
        }
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
            var localPath = Path.Combine(tempDir, $"{image.Id}.jpg");
            await _blobService.DownloadFileAsync(image.BlobUri.AbsolutePath, localPath);
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