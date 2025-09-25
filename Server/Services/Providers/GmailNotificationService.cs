using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using MimeKit;

namespace SmartCollectAPI.Services.Providers;

public class GmailNotificationService : INotificationService
{
    private readonly ILogger<GmailNotificationService> _logger;
    private readonly GmailOptions _options;
    private GmailService? _gmailService;

    public GmailNotificationService(ILogger<GmailNotificationService> logger, IOptions<GmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        InitializeGmailService();
    }

    private void InitializeGmailService()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                _logger.LogWarning("Gmail OAuth credentials not configured, Gmail service will not be available");
                return;
            }

            // For production, you would implement proper OAuth flow
            // For now, we'll use a service account or stored credentials
            
            if (!string.IsNullOrWhiteSpace(_options.CredentialsPath) && File.Exists(_options.CredentialsPath))
            {
                // Use service account credentials if available
                var serviceAccountCredential = GoogleCredential.FromFile(_options.CredentialsPath)
                    .CreateScoped(GmailService.Scope.GmailSend);
                
                _gmailService = new GmailService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = serviceAccountCredential,
                    ApplicationName = "SmartCollectAPI"
                });
            }
            else
            {
                _logger.LogWarning("Gmail credentials file not found, Gmail service will not be available");
                return;
            }

            _logger.LogInformation("Gmail service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Gmail service");
        }
    }

    public async Task<NotificationResult> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gmailService == null)
            {
                return new NotificationResult(
                    Success: false,
                    ErrorMessage: "Gmail service not initialized. Check credentials configuration."
                );
            }

            _logger.LogInformation("Sending email notification to {ToEmail} with subject: {Subject}", 
                request.ToEmail, request.Subject);

            // Create MIME message
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress("SmartCollectAPI", _options.FromEmail ?? "noreply@smartcollect.com"));
            mimeMessage.To.Add(new MailboxAddress("", request.ToEmail));
            mimeMessage.Subject = request.Subject;

            // Create body
            var bodyBuilder = new BodyBuilder();
            if (request.IsHtml)
            {
                bodyBuilder.HtmlBody = request.Body;
            }
            else
            {
                bodyBuilder.TextBody = request.Body;
            }

            // Add attachments
            if (request.Attachments != null && request.Attachments.Any())
            {
                foreach (var attachment in request.Attachments)
                {
                    bodyBuilder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
                }
            }

            mimeMessage.Body = bodyBuilder.ToMessageBody();

            // Convert to Gmail message format
            using var stream = new MemoryStream();
            await mimeMessage.WriteToAsync(stream, cancellationToken);
            var rawMessage = Convert.ToBase64String(stream.ToArray())
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");

            var gmailMessage = new Message
            {
                Raw = rawMessage
            };

            // Send the message
            var result = await _gmailService.Users.Messages.Send(gmailMessage, "me").ExecuteAsync();

            _logger.LogInformation("Successfully sent email notification. Message ID: {MessageId}", result.Id);

            return new NotificationResult(
                Success: true,
                MessageId: result.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification via Gmail");
            return new NotificationResult(
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }
}

public class GmailOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? CredentialsPath { get; set; }
    public string? FromEmail { get; set; }
    public string? RefreshToken { get; set; }
}