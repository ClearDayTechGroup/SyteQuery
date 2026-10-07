namespace SyteQuery.Features.Notifications.Services;

/// <summary>
/// Service for displaying user notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Event fired when a notification is raised
    /// </summary>
    event Action<Notification>? OnNotification;

    /// <summary>
    /// Show an error notification
    /// </summary>
    void ShowError(string message);

    /// <summary>
    /// Show a warning notification
    /// </summary>
    void ShowWarning(string message);

    /// <summary>
    /// Show a success notification
    /// </summary>
    void ShowSuccess(string message);

    /// <summary>
    /// Show an informational notification
    /// </summary>
    void ShowInfo(string message);
}

/// <summary>
/// Represents a notification to be displayed to the user
/// </summary>
public sealed record Notification(string Message, NotificationType Type, DateTime Timestamp)
{
    public Notification(string Message, NotificationType Type) : this(Message, Type, DateTime.Now)
    {
    }
}

/// <summary>
/// Type of notification
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
