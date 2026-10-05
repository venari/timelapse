namespace timelapse.core.models;

public class AggregatedTelemetry
{
    public DateTime BucketStart { get; set; }
    public DateTime BucketEnd { get; set; }
    public int DataPointCount { get; set; }

    // Temperature aggregates
    public int TemperatureC_Min { get; set; }
    public int TemperatureC_Max { get; set; }
    public double TemperatureC_Avg { get; set; }

    // Battery percentage aggregates
    public int BatteryPercent_Min { get; set; }
    public int BatteryPercent_Max { get; set; }
    public double BatteryPercent_Avg { get; set; }

    // Disk space aggregates (nullable)
    public int? DiskSpaceFree_Min { get; set; }
    public int? DiskSpaceFree_Max { get; set; }
    public double? DiskSpaceFree_Avg { get; set; }

    // Uptime aggregates (nullable)
    public int? UptimeSeconds_Min { get; set; }
    public int? UptimeSeconds_Max { get; set; }
    public double? UptimeSeconds_Avg { get; set; }

    // Pending images aggregates (nullable)
    public int? PendingImages_Min { get; set; }
    public int? PendingImages_Max { get; set; }
    public double? PendingImages_Avg { get; set; }

    // Uploaded images aggregates (nullable)
    public int? UploadedImages_Min { get; set; }
    public int? UploadedImages_Max { get; set; }
    public double? UploadedImages_Avg { get; set; }

    // Pending telemetry aggregates (nullable)
    public int? PendingTelemetry_Min { get; set; }
    public int? PendingTelemetry_Max { get; set; }
    public double? PendingTelemetry_Avg { get; set; }

    // Uploaded telemetry aggregates (nullable)
    public int? UploadedTelemetry_Min { get; set; }
    public int? UploadedTelemetry_Max { get; set; }
    public double? UploadedTelemetry_Avg { get; set; }

    // Battery voltage aggregates (nullable)
    public int? BatteryVoltage_Min { get; set; }
    public int? BatteryVoltage_Max { get; set; }
    public double? BatteryVoltage_Avg { get; set; }

    // Battery current aggregates (nullable)
    public int? BatteryCurrent_Min { get; set; }
    public int? BatteryCurrent_Max { get; set; }
    public double? BatteryCurrent_Avg { get; set; }

    // Representative status (most common or first in bucket)
    public string? Status { get; set; }
    public bool? Charging { get; set; }
}
