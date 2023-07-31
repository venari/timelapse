using System.ComponentModel.DataAnnotations;

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

    public string FixUpInvalidPiJuiceJSONStatus {get {
        if(Status!=null){
            var status = Status;
            status = status.Replace("'", "\"");
            status = status.Replace(": False", ": \"False\"");
            status = status.Replace(": True", ": \"True\"");
            return status;
        }
        return null;
    }}

    private dynamic PiJuiceJSONStatus {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                dynamic status2 = System.Text.Json.JsonSerializer.Deserialize<dynamic>(status.GetProperty("status"));
                return status2;
            }

            return null;
        }
    }


    public int? BatteryVoltage {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                int batteryVoltage = System.Text.Json.JsonSerializer.Deserialize<int>(status.GetProperty("batteryVoltage"));
                return batteryVoltage;
            }

            return null;
        }
    }

    public int? BatteryCurrent {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                int batteryCurrent = System.Text.Json.JsonSerializer.Deserialize<int>(status.GetProperty("batteryCurrent"));
                return batteryCurrent;
            }

            return null;
        }
    }

    public int? IOVoltage {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                int ioVoltage = System.Text.Json.JsonSerializer.Deserialize<int>(status.GetProperty("ioVoltage"));
                return ioVoltage;
            }

            return null;
        }
    }

    public int? IOCurrent {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                int ioCurrent = System.Text.Json.JsonSerializer.Deserialize<int>(status.GetProperty("ioCurrent"));
                return ioCurrent;
            }

            return null;
        }
    }


    public string? Status_Battery {
        get{
            if(Status!=null){
                dynamic status = PiJuiceJSONStatus;
                return status.GetProperty("battery").ToString()
                    .Replace("CHARGING_FROM_IN", "Charging")
                    .Replace("CHARGING_FROM_5V_IO", "Charging")
                    .Replace("NOT_PRESENT", "Not Present")
                    .Replace("NORMAL", "Normal");
            }

            return null;
        }
    }

    public bool? Charging {
        get{
            if(Status_Battery == "Charging"){
                return true;
            } else {
                return null;
            }
        }
    }

    public string? Status_PowerInput {
        get{
            if(Status!=null){
                dynamic status = PiJuiceJSONStatus;
                return status.GetProperty("powerInput").ToString()
                    .Replace("WEAK", "Weak")
                    .Replace("BAD", "Bad")
                    .Replace("NOT_PRESENT", "Not Present")
                    .Replace("PRESENT", "Present");
            }

            return null;
        }
    }
}
