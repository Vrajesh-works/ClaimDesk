using ClaimDesk.Domain.Claims;

namespace ClaimDesk.Application.Abstractions;

/// <summary>Delivery channel for notifications (log today; email or Service Bus later).</summary>
public interface INotificationSink
{
    Task PublishAsync(Notification notification, CancellationToken cancellationToken);
}
