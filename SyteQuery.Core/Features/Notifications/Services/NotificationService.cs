namespace SyteQuery.Features.Notifications.Services;

/// <summary>
/// Implementation of notification service for user-facing messages
/// </summary>
public class NotificationService : INotificationService
{
    /// <summary>
    /// Event fired when a notification is raised
    /// </summary>
    public event Action<Notification>? OnNotification;

    /// <summary>
    /// Show an error notification
    /// </summary>
    public void ShowError(string message)
    {
        OnNotification?.Invoke(new Notification(message, NotificationType.Error));
    }

    /// <summary>
    /// Show a warning notification
    /// </summary>
    public void ShowWarning(string message)
    {
        OnNotification?.Invoke(new Notification(message, NotificationType.Warning));
    }

    /// <summary>
    /// Show a success notification
    /// </summary>
    public void ShowSuccess(string message)
    {
        OnNotification?.Invoke(new Notification(message, NotificationType.Success));
    }

    /// <summary>
    /// Show an informational notification
    /// </summary>
    public void ShowInfo(string message)
    {
        OnNotification?.Invoke(new Notification(message, NotificationType.Info));
    }
}
