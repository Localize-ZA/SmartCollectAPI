using System.ComponentModel.DataAnnotations;

namespace smtp.Models;

public class EmailRequest
{
    [Required]
    [EmailAddress]
    public string To { get; set; } = string.Empty;
    
    [EmailAddress]
    public string? Cc { get; set; }
    
    [EmailAddress]
    public string? Bcc { get; set; }
    
    [Required]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string Body { get; set; } = string.Empty;
    
    public bool IsHtml { get; set; } = false;
    
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
    
    public List<EmailAttachment>? Attachments { get; set; }
}

public class EmailAttachment
{
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string ContentType { get; set; } = string.Empty;
    
    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public enum EmailPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public class EmailResponse
{
    public string MessageId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime SentAt { get; set; }
}

public class EmailTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public Dictionary<string, object>? Variables { get; set; }
}

public class TemplateEmailRequest
{
    [Required]
    [EmailAddress]
    public string To { get; set; } = string.Empty;
    
    [EmailAddress]
    public string? Cc { get; set; }
    
    [EmailAddress]
    public string? Bcc { get; set; }
    
    [Required]
    public string TemplateName { get; set; } = string.Empty;
    
    public Dictionary<string, object>? Variables { get; set; }
    
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
}