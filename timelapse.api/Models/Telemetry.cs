using System.ComponentModel.DataAnnotations;

namespace timelapse.api{
    
    public class TelemetryPostModel
    {
        [Required]
        public int DeviceId {get; set;}
        public DateTime Timestamp {get; set;}
        [Required]
        public int TemperatureC {get; set;}
        [Required]
        public int BatteryPercent {get; set;}
        [Required]
        public int DiskSpaceFree {get; set;}
        [Required]
        public int UptimeSeconds {get; set;}
    }
}