using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

public partial class MainWindow
{
    private TaskCompletionSource<bool>? nativeUpdateDecision;

    /// <summary>Confirmation hosted inside Arcane Vault instead of a Windows
    /// MessageBox. All WPF access remains on the dispatcher thread.</summary>
    private async Task<bool> ConfirmNativeUpdateAsync(string title,string message,
        string approveLabel)
    {
        if(closing || nativeUpdateDecision is not null)return false;
        var completion=new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        nativeUpdateDecision=completion;
        NativeUpdatePromptTitle.Text=title;
        NativeUpdatePromptMessage.Text=message;
        NativeUpdatePromptApprove.Content=approveLabel;
        NativeUpdatePromptOverlay.Visibility=Visibility.Visible;
        NativeUpdatePromptApprove.Focus();
        try{return await completion.Task;}
        finally
        {
            NativeUpdatePromptOverlay.Visibility=Visibility.Collapsed;
            nativeUpdateDecision=null;
        }
    }

    private void ResolveNativeUpdatePrompt(bool accepted)
    {
        nativeUpdateDecision?.TrySetResult(accepted);
    }

    private void OnNativeUpdatePromptCancel(object sender,RoutedEventArgs e) =>
        ResolveNativeUpdatePrompt(false);

    private void OnNativeUpdatePromptApprove(object sender,RoutedEventArgs e) =>
        ResolveNativeUpdatePrompt(true);
}
