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
            var functionUrl = _configuration["AzureFunctions:TimelapseUrl"];
            var functionKey = _configuration["AzureFunctions:TimelapseKey"];
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{functionUrl}?code={functionKey}", 
                request);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new { success = true, message = "Timelapse creation started" });
            }

            return BadRequest(new { success = false, message = "Failed to start timelapse creation" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Internal server error" });
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