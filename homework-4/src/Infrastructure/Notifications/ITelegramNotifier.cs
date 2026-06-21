namespace Infrastructure.Notifications;

public interface ITelegramNotifier
{
    Task NotifyError(string message, CancellationToken cancellationToken = default);
}
