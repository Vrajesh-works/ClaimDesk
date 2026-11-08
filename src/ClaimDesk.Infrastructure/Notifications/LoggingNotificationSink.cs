using ClaimDesk.Application.Abstractions;
using ClaimDesk.Domain.Claims;
using Microsoft.Extensions.Logging;

namespace ClaimDesk.Infrastructure.Notifications;

/// <summary>Writes notifications to the structured log. Email or Service Bus delivery is future work.</summary>
public sealed class LoggingNotificationSink(ILogger<LoggingNotificationSink> logger) : INotificationSink
{
    public Task PublishAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        logger.LogWarning(
            "Notification {Kind} for {Role} on claim {ClaimId}: {Message}",
            notification.Kind,
            notification.RecipientRole,
            notification.ClaimId,
            notification.Message);
        return Task.CompletedTask;
    }
}
