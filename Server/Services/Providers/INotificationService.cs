namespace SmartCollectAPI.Services.Providers;

public interface INotificationService
{
    Task<NotificationResult> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

public record NotificationRequest(
    string ToEmail,
    string Subject,
    string Body,
    List<NotificationAttachment>? Attachments = null,
    bool IsHtml = true
);

public record NotificationAttachment(
    string FileName,
    string ContentType,
    byte[] Content
);

public record NotificationResult(
    bool Success,
    string? MessageId = null,
    string? ErrorMessage = null
);