namespace NotificationService.Domain.Exceptions;

public sealed class NotificationException : Exception
{
    public NotificationException(string message) : base(message) { }
    public NotificationException(string message, Exception inner) : base(message, inner) { }
}

public sealed class NotificationNotFoundException : Exception
{
    public NotificationNotFoundException(Guid id)
        : base($"Notification {id} was not found.") { }
}
