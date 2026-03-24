namespace AuthManager.UI.Components.Shared;

public enum ToastLevel { Info, Success, Warning, Error }

public sealed record ToastItem(string Message, string? Title, ToastLevel Level, int DurationMs = 4000);

/// <summary>
/// Simple singleton toast service for in-component notifications.
/// </summary>
public sealed class ToastService
{
    public static readonly ToastService Instance = new();

    public event Action<ToastItem>? OnToast;

    private ToastService() { }

    public void Show(string message, ToastLevel level = ToastLevel.Info, string? title = null, int durationMs = 4000)
        => OnToast?.Invoke(new ToastItem(message, title, level, durationMs));

    public void Success(string message, string? title = "Success") => Show(message, ToastLevel.Success, title);
    public void Error(string message, string? title = "Error") => Show(message, ToastLevel.Error, title);
    public void Warning(string message, string? title = "Warning") => Show(message, ToastLevel.Warning, title);
    public void Info(string message, string? title = null) => Show(message, ToastLevel.Info, title);
}
