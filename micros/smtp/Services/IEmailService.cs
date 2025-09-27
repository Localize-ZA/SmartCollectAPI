using smtp.Models;

namespace smtp.Services;

public interface IEmailService
{
    Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken = default);
    Task<EmailResponse> SendTemplateEmailAsync(TemplateEmailRequest request, CancellationToken cancellationToken = default);
    Task<BulkEmailResponse> SendBulkEmailsAsync(BulkEmailRequest request, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    string GetSmtpHost();
}

public interface ITemplateService
{
    Task<EmailTemplate?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default);
    Task<string> RenderTemplateAsync(string template, Dictionary<string, object>? variables, CancellationToken cancellationToken = default);
    Task<List<string>> GetAvailableTemplatesAsync(CancellationToken cancellationToken = default);
    Task<List<EmailTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);
}