using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace timelapse.core.models;

public class Telemetry
{
    public int Id {get; set;}
    [Required]
    public DateTime Timestamp {get; set;}
    [Required]
    public int TemperatureC {get; set;}
    [Required]
    public int BatteryPercent {get; set;}

    public string? Status {get; set;}

    public int? DiskSpaceFree {get; set;}
    public int? UptimeSeconds {get; set;}
    public int? PendingImages {get; set;}
    public int? UploadedImages {get; set;}
    public int? PendingTelemetry {get; set;}
    public int? UploadedTelemetry {get; set;}

    [Required]
    public int DeviceId {get; set;}
    public Device Device {get; set;}

    // The Raspberry Pi's saveTelemetry.py writes a Python dict repr rather than valid JSON
    // (single quotes, True/False/None). These replacements are no-ops for ESP32 firmware, which
    // already sends well-formed JSON (e.g. {"boot_count":1,"voltage_mv":3940,"solar_voltage_mv":6}).
    public string? FixUpInvalidPiJuiceJSONStatus {get {
        if(Status!=null){
            var status = Status;
            status = status.Replace("'", "\"");
            status = status.Replace(": False", ": \"False\"");
            status = status.Replace(": True", ": \"True\"");
            status = status.Replace(": None", ": null");
            return status;
        }
        return null;
    }}

    // Parses Status defensively. Returns null - never throws - if Status is missing, blank, or
    // not valid JSON once fixed up, so a device sending an unexpected/truncated status doesn't
    // take down every other Telemetry property.
    private JsonElement? ParsedStatus {
        get {
            var fixedUpStatus = FixUpInvalidPiJuiceJSONStatus;
            if (string.IsNullOrWhiteSpace(fixedUpStatus)) {
                return null;
            }

            try {
                return JsonSerializer.Deserialize<JsonElement>(fixedUpStatus);
            } catch (JsonException) {
                return null;
            }
        }
    }

    // Raspberry Pi devices nest PiJuice detail under a "status" property, e.g.
    // {"status": {"chargeState": "...", "battery": "...", "powerInput": "..."}, "batteryVoltage": ...}.
    // ESP32 devices don't have one at all - returns null for either an absent property or a
    // value that isn't an object.
    private JsonElement? PiJuiceStatusDetail {
        get {
            var status = ParsedStatus;
            if (status is not { ValueKind: JsonValueKind.Object } root) {
                return null;
            }

            if (!root.TryGetProperty("status", out var detail) || detail.ValueKind != JsonValueKind.Object) {
                return null;
            }

            return detail;
        }
    }

    private static int? TryGetInt(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } obj) {
            return null;
        }

        if (!obj.TryGetProperty(propertyName, out var value)) {
            return null;
        }

        return value.ValueKind switch {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static string? TryGetString(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } obj) {
            return null;
        }

        if (!obj.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String) {
            return null;
        }

        return value.GetString();
    }

    // Pi-only fields (ESP32 status has no "batteryVoltage"/"batteryCurrent"/"ioVoltage"/"ioCurrent") -
    // May be recorded for Lilygo T-SIM ESP32-S3 devices
    // return null if missing rather than throwing.
    public int? BatteryVoltage {
        get {
            int? voltage = TryGetInt(ParsedStatus, "batteryVoltage");
            
            if(voltage == null) {
                voltage = TryGetInt(ParsedStatus, "voltage_mv");
            }

            return voltage;
        }
    }

    public int? BatteryCurrent => TryGetInt(ParsedStatus, "batteryCurrent");

    public int? IOVoltage {
        get {
            int? ioVoltage = TryGetInt(ParsedStatus, "ioVoltage");
            
            if(ioVoltage == null) {
                ioVoltage = TryGetInt(ParsedStatus, "solar_voltage_mv");
            }
    
            return ioVoltage;
        }
    }

    public int? IOCurrent => TryGetInt(ParsedStatus, "ioCurrent");

    public bool? PowerSwitch {
        get{
            var status = ParsedStatus;
            if (status is not { ValueKind: JsonValueKind.Object } root) {
                return null;
            }

            if (!root.TryGetProperty("powerSwitch", out var powerSwitch) || powerSwitch.ValueKind != JsonValueKind.Object) {
                return null;
            }

            if (!powerSwitch.TryGetProperty("data", out var powerSwitchData) || powerSwitchData.ValueKind != JsonValueKind.Number) {
                return null;
            }

            return powerSwitchData.GetInt32() > 0 ? true : null;
        }
    }

    public bool? ConnectedToWirelessNetwork => TryGetString(ParsedStatus, "connectedToWirelessNetwork") == "True" ? true : null;

    public string? WirelessSSID => TryGetString(ParsedStatus, "wirelessSSID");

    public bool? ConnectedToInternet => TryGetString(ParsedStatus, "connectedToInternet") == "True" ? true : null;

    public string? Status_Battery {
        get {
            var battery = TryGetString(PiJuiceStatusDetail, "battery");
            if (battery == null) {
                return null;
            }

            return battery
                .Replace("CHARGING_FROM_IN", "Charging")
                .Replace("CHARGING_FROM_5V_IO", "Charging")
                .Replace("NOT_PRESENT", "Not Present")
                .Replace("NORMAL", "Normal");
        }
    }

    public string Charge_State {
        get {
            var chargeState = TryGetString(PiJuiceStatusDetail, "chargeState");
            if (chargeState == null) {
                return "Unknown";
            }

            return chargeState
                .Replace("Trickle Charge (VBAT < VBAT_SHORT)", "Charging")
                .Replace("Pre-Charge (VBAT < VBAT_LOWV)", "Charging")
                .Replace("Fast Charge (CC mode)", "Charging")
                .Replace("Taper Charge (CV mode)", "Charging")
                .Replace("Top-off Timer Charge", "Charging")
                .Replace("Charge Termination Done", "Not charging");
        }
    }

    public bool? Charging => (Status_Battery == "Charging" || Charge_State == "Charging") ? true : null;

    public string? Status_PowerInput {
        get{
            var powerInput = TryGetString(PiJuiceStatusDetail, "powerInput");
            if (powerInput == null) {
                return null;
            }

            return powerInput
                .Replace("WEAK", "Weak")
                .Replace("BAD", "Bad")
                .Replace("NOT_PRESENT", "Not Present")
                .Replace("PRESENT", "Present");
        }
    }
}
