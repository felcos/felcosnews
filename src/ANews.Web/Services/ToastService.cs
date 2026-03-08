namespace ANews.Web.Services;

public enum ToastType { Success, Error, Warning, Info }

public record ToastMessage(string Message, ToastType Type, DateTime CreatedAt);

public class ToastService
{
    public event Action<ToastMessage>? OnToast;
    public void Show(string message, ToastType type = ToastType.Success) =>
        OnToast?.Invoke(new ToastMessage(message, type, DateTime.Now));
    public void Success(string msg) => Show(msg, ToastType.Success);
    public void Error(string msg) => Show(msg, ToastType.Error);
    public void Warning(string msg) => Show(msg, ToastType.Warning);
    public void Info(string msg) => Show(msg, ToastType.Info);
}
