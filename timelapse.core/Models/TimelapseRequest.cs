// filepath: /home/leigh/dev/timelapse/timelapse.functions/Models/TimelapseRequest.cs
namespace timelapse.core.models;

public class TimelapseRequest
{
    public int? EventId { get; set; }
    public int? DeviceId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string RequestedByUserId { get; set; }
    public string Description { get; set; }
}

public class TimelapseResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string QueueId { get; set; }
}