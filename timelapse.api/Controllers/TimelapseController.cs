using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class TimelapseController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TimelapseController> _logger;

    public TimelapseController(HttpClient httpClient, IConfiguration configuration, ILogger<TimelapseController> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateTimelapse([FromBody] TimelapseCreateRequest request)
    {
        try
        {
            Console.WriteLine($"=== CONTROLLER: Received request - EventId: {request?.EventId}, RequestedBy: {request?.RequestedByUserId} ===");
            _logger.LogWarning("=== Received CreateTimelapse request in controller - EventId: {EventId}, RequestedBy: {RequestedBy} ===", 
                request?.EventId, request?.RequestedByUserId);
            
            // For local development, point to local function
            var functionUrl = _configuration["AzureFunctions:TimelapseUrl"] ?? "http://localhost:7071/api/CreateTimelapse";
            var functionKey = _configuration["AzureFunctions:TimelapseKey"]; // Not needed for local
            
            var url = string.IsNullOrEmpty(functionKey) ? functionUrl : $"{functionUrl}?code={functionKey}";
            
            Console.WriteLine($"=== CONTROLLER: Forwarding to: {url} ===");
            _logger.LogWarning("Forwarding to Azure Function at: {Url}", url);
            
            // Serialize the request and log it
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var jsonContent = JsonSerializer.Serialize(request, jsonOptions);
            Console.WriteLine($"=== CONTROLLER: Sending JSON: {jsonContent} ===");
            _logger.LogWarning("Sending JSON: {Json}", jsonContent);
            
            // Create StringContent with the JSON
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            
            Console.WriteLine($"=== CONTROLLER: Function responded with: {response.StatusCode} ===");
            _logger.LogWarning("Azure Function responded with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Ok(new { success = true, message = "Timelapse creation started", details = result });
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return BadRequest(new { success = false, message = "Failed to start timelapse creation", error = errorContent });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Internal server error", error = ex.Message });
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