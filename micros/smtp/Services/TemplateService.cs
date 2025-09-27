using smtp.Models;
using smtp.Services;
using System.Text.RegularExpressions;

namespace smtp.Services;

public class TemplateService : ITemplateService
{
    private readonly ILogger<TemplateService> _logger;
    private readonly Dictionary<string, EmailTemplate> _templates;

    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger;
        _templates = InitializeDefaultTemplates();
    }

    public Task<EmailTemplate?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        _templates.TryGetValue(templateName.ToLowerInvariant(), out var template);
        return Task.FromResult(template);
    }

    public Task<string> RenderTemplateAsync(string template, Dictionary<string, object>? variables, CancellationToken cancellationToken = default)
    {
        if (variables == null || variables.Count == 0)
        {
            return Task.FromResult(template);
        }

        var result = template;
        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            result = result.Replace(placeholder, variable.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return Task.FromResult(result);
    }

    public Task<List<string>> GetAvailableTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_templates.Keys.ToList());
    }

    private Dictionary<string, EmailTemplate> InitializeDefaultTemplates()
    {
        return new Dictionary<string, EmailTemplate>
        {
            ["welcome"] = new EmailTemplate
            {
                Name = "welcome",
                Subject = "Welcome to {{CompanyName}}!",
                HtmlBody = @"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h1 style='color: #2c3e50;'>Welcome {{UserName}}!</h1>
                            <p>Thank you for joining {{CompanyName}}. We're excited to have you on board!</p>
                            <p>Your account has been successfully created with the email: <strong>{{Email}}</strong></p>
                            <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <h3>What's next?</h3>
                                <ul>
                                    <li>Complete your profile</li>
                                    <li>Explore our features</li>
                                    <li>Connect with our community</li>
                                </ul>
                            </div>
                            <p>If you have any questions, feel free to reach out to our support team.</p>
                            <p>Best regards,<br>The {{CompanyName}} Team</p>
                        </div>
                    </body>
                    </html>",
                TextBody = @"Welcome {{UserName}}!

Thank you for joining {{CompanyName}}. We're excited to have you on board!

Your account has been successfully created with the email: {{Email}}

What's next?
- Complete your profile
- Explore our features
- Connect with our community

If you have any questions, feel free to reach out to our support team.

Best regards,
The {{CompanyName}} Team"
            },

            ["notification"] = new EmailTemplate
            {
                Name = "notification",
                Subject = "{{Subject}}",
                HtmlBody = @"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #2c3e50;'>{{Title}}</h2>
                            <p>{{Message}}</p>
                            {{#if ActionUrl}}
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{{ActionUrl}}' style='background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>{{ActionText}}</a>
                            </div>
                            {{/if}}
                            <p>Best regards,<br>{{CompanyName}}</p>
                        </div>
                    </body>
                    </html>",
                TextBody = @"{{Title}}

{{Message}}

{{#if ActionUrl}}
{{ActionText}}: {{ActionUrl}}
{{/if}}

Best regards,
{{CompanyName}}"
            },

            ["document-processed"] = new EmailTemplate
            {
                Name = "document-processed",
                Subject = "Document Processing Complete - {{DocumentName}}",
                HtmlBody = @"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #27ae60;'>Document Processing Complete</h2>
                            <p>Hello {{UserName}},</p>
                            <p>Your document <strong>{{DocumentName}}</strong> has been successfully processed.</p>
                            <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <h3>Processing Details:</h3>
                                <ul>
                                    <li><strong>Document ID:</strong> {{DocumentId}}</li>
                                    <li><strong>File Name:</strong> {{DocumentName}}</li>
                                    <li><strong>Processing Status:</strong> {{Status}}</li>
                                    <li><strong>Processed At:</strong> {{ProcessedAt}}</li>
                                </ul>
                            </div>
                            <p>You can view the processed document in your dashboard.</p>
                            <p>Best regards,<br>SmartCollect Team</p>
                        </div>
                    </body>
                    </html>",
                TextBody = @"Document Processing Complete

Hello {{UserName}},

Your document {{DocumentName}} has been successfully processed.

Processing Details:
- Document ID: {{DocumentId}}
- File Name: {{DocumentName}}
- Processing Status: {{Status}}
- Processed At: {{ProcessedAt}}

You can view the processed document in your dashboard.

Best regards,
SmartCollect Team"
            }
        };
    }
}