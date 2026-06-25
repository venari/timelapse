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
            status = status.Replace(": None", ": null");
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

    public bool? PowerSwitch {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                
                if (!status.TryGetProperty("powerSwitch", out System.Text.Json.JsonElement powerSwitch))
                {
                    return null;
                }

                if (!powerSwitch.TryGetProperty("data", out System.Text.Json.JsonElement powerSwitchData))
                {
                    return null;
                }

                return powerSwitchData.GetInt32() > 0?true:null;
            }

            return null;
        }
    }

    public bool? ConnectedToWirelessNetwork {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                if (!status.TryGetProperty("connectedToWirelessNetwork", out System.Text.Json.JsonElement connectedToWireless))
                {
                    return null;
                }

                return connectedToWireless.GetString()=="True"?true:null;
            }

            return null;
        }
    }

    public string? WirelessSSID {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                if (!status.TryGetProperty("wirelessSSID", out System.Text.Json.JsonElement wirelessSSID))
                {
                    return null;
                }
                return wirelessSSID.GetString();
            }

            return null;
        }
    }

    public bool? ConnectedToInternet {
        get{
            if(Status!=null){
                dynamic status = System.Text.Json.JsonSerializer.Deserialize<dynamic>(FixUpInvalidPiJuiceJSONStatus);
                if (!status.TryGetProperty("connectedToInternet", out System.Text.Json.JsonElement connectedToInternet))
                {
                    return null;
                }
                return connectedToInternet.GetString() == "True"?true:null;
            }

            return null;
        }
    }

    public string? Status_Battery
    {
        get
        {
            if (Status != null)
            {
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

    public string Charge_State
    {
        get
        {
            if (Status != null)
            {
                dynamic status = PiJuiceJSONStatus;
                if(!status.TryGetProperty("chargeState", out System.Text.Json.JsonElement chargeState))
                {
                    return "Unknown";
                }

                return chargeState.GetString()
                    .Replace("Not charging", "Not charging")
                    .Replace("Trickle Charge (VBAT < VBAT_SHORT)", "Charging")
                    .Replace("Pre-Charge (VBAT < VBAT_LOWV)", "Charging")
                    .Replace("Fast Charge (CC mode)", "Charging")
                    .Replace("Taper Charge (CV mode)", "Charging")
                    .Replace("NA", "NA")
                    .Replace("Top-off Timer Charge", "Charging")
                    .Replace("Charge Termination Done", "Not charging");
            }

            return "Unknown";
        }
    }

    public bool? Charging {
        get{
            if(Status_Battery == "Charging" || Charge_State == "Charging"){
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
