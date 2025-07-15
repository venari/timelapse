using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace timelapse.Services;

public class EmailSender : IEmailSender
{
    private readonly ILogger _logger;

    public EmailSender(IOptions<AuthMessageSenderOptions> optionsAccessor,
                       ILogger<EmailSender> logger)
    {
        Options = optionsAccessor.Value;
        _logger = logger;
    }

    public AuthMessageSenderOptions Options { get; } //Set with Secret Manager.

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        _logger.LogInformation($"Sending email to {toEmail}");
        if (string.IsNullOrEmpty(Options.SendGridAPIKey))
        {
            throw new Exception("Null SendGridAPIKey");
        }
        if (string.IsNullOrEmpty(Options.SendGridFromAddress))
        {
            throw new Exception("Null SendGridFromAddress");
        }
        await Execute(Options.SendGridAPIKey, subject, message, toEmail, Options.SendGridFromAddress, Options.SendGridFromName);
    }

    public async Task Execute(string apiKey, string subject, string message, string toEmail, string fromEmail, string fromName)
    {
        var client = new SendGridClient(apiKey);
        var msg = new SendGridMessage()
        {
            From = new EmailAddress(fromEmail, fromName),
            Subject = subject,
            PlainTextContent = message,
            HtmlContent = message
        };
        msg.AddTo(new EmailAddress(toEmail));

        // Disable click tracking.
        // See https://sendgrid.com/docs/User_Guide/Settings/tracking.html
        msg.SetClickTracking(false, false);
        var response = await client.SendEmailAsync(msg);
        _logger.LogInformation(response.IsSuccessStatusCode 
                               ? $"Email to {toEmail} queued successfully!"
                               : $"Failure Email to {toEmail}");
    }
}