using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Routine OK-only information and warnings are nonmodal in-app notices.
/// Any answer that changes what the application does (Yes/No, OK/Cancel,
/// destructive confirmations) remains a blocking confirmation dialog.
/// </summary>
public static class NativeInAppDialog
{
    public static MessageBoxResult Show(DependencyObject context, string message,
        string caption, MessageBoxButton buttons, MessageBoxImage icon)
    {
        if (buttons == MessageBoxButton.OK)
        {
            NativeNoticeHub.Publish(caption, message,
                icon is MessageBoxImage.Error or MessageBoxImage.Warning);
            return MessageBoxResult.OK;
        }

        Window? owner = context as Window ?? Window.GetWindow(context);
        return owner is null
            ? MessageBox.Show(message, caption, buttons, icon)
            : MessageBox.Show(owner, message, caption, buttons, icon);
    }
}
