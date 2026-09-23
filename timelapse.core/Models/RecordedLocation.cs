using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

// A raw GPS fix reported by a device (see Telemetry.GeoLatitude/GeoLongitude/GeoTimeRecorded,
// parsed from the "geo.lat"/"geo.lon"/"geo.time-recorded" fields the ESP32 embeds in its
// Telemetry Status JSON - see updateGeoLocationIfDue() in the .ino). One row per distinct GPS fix
// (TelemetryController dedupes on Timestamp before inserting, since the device re-sends the same
// cached fix in every Telemetry post between GeoIntervalS-paced fixes).
//
// Deliberately separate from DeviceLocation, which is the curated/confirmed location shown on the
// map and used for heading/FOV - a device technician promotes one of these into a DeviceLocation
// by hand (see DeviceEdit) rather than every raw fix silently becoming the device's location.
public class RecordedLocation
{
    public int Id {get; set;}
    [Required]
    public int DeviceId {get; set;}
    [Required]
    public Device Device {get; set;}
    [Required]
    public double Latitude {get; set;}
    [Required]
    public double Longitude {get; set;}

    // The GPS fix time the device itself reported (geo.time-recorded), not when the API received
    // it - this is what TelemetryController compares against to dedupe repeated posts of the same
    // cached fix.
    [Required]
    public DateTime Timestamp {get; set;}
}
