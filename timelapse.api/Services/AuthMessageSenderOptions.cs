namespace timelapse.Services;

public class AuthMessageSenderOptions
{
    public string? SendGridAPIKey { get; set; }
    public string? SendGridFromAddress { get; set; }
    public string SendGridFromName { get; set; } = "EnviroEyes";
}