using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using smtp.Models;
using smtp.Services;
using System.Text.RegularExpressions;

namespace smtp.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ITemplateService _templateService;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration configuration,
        ITemplateService templateService,
        ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = CreateMimeMessage(request);

            using var client = new SmtpClient();
            await ConnectToSmtpServerAsync(client, cancellationToken);

            var messageId = await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {To} with Message ID: {MessageId}", request.To, messageId);

            return new EmailResponse
            {
                MessageId = messageId,
                Success = true,
                SentAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", request.To);
            return new EmailResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<EmailResponse> SendTemplateEmailAsync(TemplateEmailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(request.TemplateName, cancellationToken);
            if (template == null)
            {
                return new EmailResponse
                {
                    Success = false,
                    ErrorMessage = $"Template '{request.TemplateName}' not found",
                    SentAt = DateTime.UtcNow
                };
            }

            var renderedSubject = await _templateService.RenderTemplateAsync(template.Subject, request.Variables, cancellationToken);
            var renderedHtmlBody = await _templateService.RenderTemplateAsync(template.HtmlBody, request.Variables, cancellationToken);
            var renderedTextBody = await _templateService.RenderTemplateAsync(template.TextBody, request.Variables, cancellationToken);

            var emailRequest = new EmailRequest
            {
                To = request.To,
                Cc = request.Cc,
                Bcc = request.Bcc,
                Subject = renderedSubject,
                Body = string.IsNullOrEmpty(renderedHtmlBody) ? renderedTextBody : renderedHtmlBody,
                IsHtml = !string.IsNullOrEmpty(renderedHtmlBody),
                Priority = request.Priority
            };

            return await SendEmailAsync(emailRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send template email to {To} using template {TemplateName}", request.To, request.TemplateName);
            return new EmailResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient();
            await ConnectToSmtpServerAsync(client, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP connection test failed");
            return false;
        }
    }

    private MimeMessage CreateMimeMessage(EmailRequest request)
    {
        var message = new MimeMessage();
        
        // From address
        var fromEmail = _configuration["Smtp:FromEmail"] ?? "info@paratech.top";
        var fromName = _configuration["Smtp:FromName"] ?? "Paratech";
        message.From.Add(new MailboxAddress(fromName, fromEmail));

        // To addresses
        foreach (var email in request.To.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            message.To.Add(MailboxAddress.Parse(email.Trim()));
        }

        // CC addresses
        if (!string.IsNullOrEmpty(request.Cc))
        {
            foreach (var email in request.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                message.Cc.Add(MailboxAddress.Parse(email.Trim()));
            }
        }

        // BCC addresses
        if (!string.IsNullOrEmpty(request.Bcc))
        {
            foreach (var email in request.Bcc.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                message.Bcc.Add(MailboxAddress.Parse(email.Trim()));
            }
        }

        message.Subject = request.Subject;

        // Priority
        message.Priority = request.Priority switch
        {
            EmailPriority.High => MessagePriority.Urgent,
            EmailPriority.Low => MessagePriority.NonUrgent,
            _ => MessagePriority.Normal
        };

        // Body
        var builder = new BodyBuilder();
        
        if (request.IsHtml)
        {
            builder.HtmlBody = request.Body;
        }
        else
        {
            builder.TextBody = request.Body;
        }

        // Attachments
        if (request.Attachments != null)
        {
            foreach (var attachment in request.Attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    private async Task ConnectToSmtpServerAsync(SmtpClient client, CancellationToken cancellationToken)
    {
        var host = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("SMTP Host not configured");
        var port = int.Parse(_configuration["Smtp:Port"] ?? "587");
        var useSsl = bool.Parse(_configuration["Smtp:UseSsl"] ?? "true");
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];

        var secureSocketOptions = useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        
        await client.ConnectAsync(host, port, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            await client.AuthenticateAsync(username, password, cancellationToken);
        }
    }

    public async Task<BulkEmailResponse> SendBulkEmailsAsync(BulkEmailRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<EmailResult>();
        var successCount = 0;
        var failureCount = 0;

        foreach (var recipient in request.Recipients)
        {
            try
            {
                var emailRequest = new EmailRequest
                {
                    To = recipient,
                    Subject = request.Subject,
                    Body = request.Body,
                    IsHtml = request.IsHtml,
                    Priority = request.Priority
                };

                var response = await SendEmailAsync(emailRequest, cancellationToken);
                
                if (response.Success)
                {
                    successCount++;
                    results.Add(new EmailResult
                    {
                        Recipient = recipient,
                        Success = true,
                        MessageId = response.MessageId
                    });
                }
                else
                {
                    failureCount++;
                    results.Add(new EmailResult
                    {
                        Recipient = recipient,
                        Success = false,
                        ErrorMessage = response.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                results.Add(new EmailResult
                {
                    Recipient = recipient,
                    Success = false,
                    ErrorMessage = ex.Message
                });
                _logger.LogError(ex, "Failed to send bulk email to {Recipient}", recipient);
            }
        }

        return new BulkEmailResponse
        {
            SuccessCount = successCount,
            FailureCount = failureCount,
            Results = results
        };
    }

    public string GetSmtpHost()
    {
        try
        {
            return _configuration["Smtp:Host"] ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}