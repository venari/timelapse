using System.ComponentModel.DataAnnotations;

namespace timelapse.api{
    
    public class TelemetryPostModel
    {
        [Required]
        public string SerialNumber {get; set;}
        public DateTime? Timestamp {get; set;}
        [Required]
        public int TemperatureC {get; set;}
        [Required]
        public int BatteryPercent {get; set;}
        
        public string? Status {get; set;}
        public int? DiskSpaceFree {get; set;}
        public int? UptimeSeconds {get; set;}
    }
}