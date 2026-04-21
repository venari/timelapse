using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using timelapse.functions.services;

namespace timelapse.functions;

public class TestDbConnection
{
    private readonly ILogger<TestDbConnection> _logger;
    private readonly IEventService _eventService;

    public TestDbConnection(ILogger<TestDbConnection> logger, IEventService eventService)
    {
        _logger = logger;
        _eventService = eventService;
    }

    [Function("TestDbConnection")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("CreateTimelapse function processed a request.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<TimelapseCreateRequest>(requestBody);

            if (request?.EventId == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("EventId is required");
                return badResponse;
            }

            // Test database connection by fetching the event
            var eventData = await _eventService.GetEventAsync(request.EventId.Value);
            
            _logger.LogInformation("Found event: {EventId}, Device: {DeviceName}, Start: {StartTime}", 
                eventData.Id, eventData.Device?.Name, eventData.StartTime);

            // Update status to pending
            await _eventService.UpdateTimelapseStatusAsync(request.EventId.Value, "Pending");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "Timelapse creation request received and event found",
                eventId = eventData.Id,
                deviceName = eventData.Device?.Name,
                startTime = eventData.StartTime,
                endTime = eventData.EndTime
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing timelapse request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Internal server error: {ex.Message}");
            return errorResponse;
        }
    }
}

public class TimelapseCreateRequest
{
    public int? EventId { get; set; }
    public int? DeviceId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string RequestedByUserId { get; set; }
}
