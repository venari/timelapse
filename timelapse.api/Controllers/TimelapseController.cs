using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class TimelapseController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TimelapseController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateTimelapse([FromBody] TimelapseCreateRequest request)
    {
        try
        {
            // For local development, point to local function
            var functionUrl = _configuration["AzureFunctions:TimelapseUrl"] ?? "http://localhost:7071/api/CreateTimelapse";
            var functionKey = _configuration["AzureFunctions:TimelapseKey"]; // Not needed for local
            
            var url = string.IsNullOrEmpty(functionKey) ? functionUrl : $"{functionUrl}?code={functionKey}";
            
            var response = await _httpClient.PostAsJsonAsync(url, request);

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