using System.ComponentModel.DataAnnotations;

namespace timelapse.api{

    public class DeviceUpdateRequest
    {
        [Required]
        public string Name {get; set;}
        public string? Description {get; set;}
        public string? ShortDescription {get; set;}

        public bool SupportMode {get; set;}
        public bool MonitoringMode {get; set;}
        public bool HibernateMode {get; set;}
        public bool PowerOff {get; set;}
        public bool Service {get; set;}
        public bool WideAngle {get; set;}
        public bool Retired {get; set;}

        public bool SleepDuringNight {get; set;}
        public int DaytimeStartsAtH {get; set;}
        public int DaytimeEndsAtH {get; set;}
        public int UtcOffsetMinutes {get; set;}
        public int CameraIntervalS {get; set;}
        public string ApiUrl {get; set;} = "";
        public bool Hflip {get; set;}
        public bool Vflip {get; set;}
        public bool EnableLongExposureAtNight {get; set;}
        public int LongExposureXclkHz {get; set;}
        public int GeoIntervalS {get; set;}
        public int AutoSyncPeriodS {get; set;}

        public DeviceLocationUpdateRequest? Location {get; set;}
    }

    public class DeviceLocationUpdateRequest
    {
        public bool LocationMoved {get; set;}
        public string? Description {get; set;}
        public double? Latitude {get; set;}
        public double? Longitude {get; set;}
        public int? Heading {get; set;}
        public int? Pitch {get; set;}
        public int? HeightMM {get; set;}
    }
}
