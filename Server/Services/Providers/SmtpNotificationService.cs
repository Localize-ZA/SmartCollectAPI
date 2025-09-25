using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace SmartCollectAPI.Services.Providers;

public class SmtpNotificationService : INotificationService
{
    private readonly ILogger<SmtpNotificationService> _logger;
    private readonly SmtpOptions _options;

    public SmtpNotificationService(ILogger<SmtpNotificationService> logger, IOptions<SmtpOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<NotificationResult> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.Host))
            {
                return new NotificationResult(
                    Success: false,
                    ErrorMessage: "SMTP host not configured"
                );
            }

            _logger.LogInformation("Sending email notification via SMTP to {ToEmail} with subject: {Subject}", 
                request.ToEmail, request.Subject);

            using var smtpClient = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_options.Username, _options.Password)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_options.FromEmail ?? "noreply@smartcollect.com", "SmartCollectAPI"),
                Subject = request.Subject,
                Body = request.Body,
                IsBodyHtml = request.IsHtml
            };

            mailMessage.To.Add(request.ToEmail);

            // Add attachments
            if (request.Attachments != null && request.Attachments.Any())
            {
                foreach (var attachment in request.Attachments)
                {
                    var stream = new MemoryStream(attachment.Content);
                    var mailAttachment = new Attachment(stream, attachment.FileName, attachment.ContentType);
                    mailMessage.Attachments.Add(mailAttachment);
                }
            }

            await smtpClient.SendMailAsync(mailMessage);

            _logger.LogInformation("Successfully sent email notification via SMTP");

            return new NotificationResult(
                Success: true,
                MessageId: Guid.NewGuid().ToString() // Generate a pseudo message ID
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification via SMTP");
            return new NotificationResult(
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }
}

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FromEmail { get; set; }
}