using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace timelapse.functions;

public class CreateTimelapse
{
    private readonly ILogger _logger;

    public CreateTimelapse(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CreateTimelapse>();
    }

    [Function("CreateTimelapse")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("CreateTimelapse function processed a request.");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Request body: {Body}", requestBody);

            var request = JsonSerializer.Deserialize<TimelapseCreateRequest>(requestBody);
            _logger.LogInformation("Parsed request: EventId={EventId}, DeviceId={DeviceId}", 
                request?.EventId, request?.DeviceId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "Timelapse creation request received",
                eventId = request?.EventId,
                deviceId = request?.DeviceId
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing timelapse request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error");
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
