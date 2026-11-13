using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class NativeOptionsWindow : Window
{
    private readonly string initialId;
    private bool initializing;
    private bool saved;

    public NativeOptionsWindow()
    {
        initializing = true;
        initialId = VaultThemeManager.CurrentId;
        InitializeComponent();
        ThemeChoice.ItemsSource = NativeThemes.All;
        ThemeChoice.SelectedValue = initialId;
        initializing = false;
        Closed += (_, _) =>
        {
            // Uncommitted live preview must not leak into application theme.
            if (!saved && VaultThemeManager.CurrentId != initialId)
                VaultThemeManager.Apply(initialId);
        };
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (initializing || ThemeChoice.SelectedValue is not string chosen) return;
        try
        {
            VaultThemeManager.Apply(chosen);
            ThemeStatus.Text = "Previewing " +
                (NativeThemes.Find(chosen)?.Label ?? chosen) +
                ". Save appearance to keep this theme.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            InvalidOperationException or FormatException)
        {
            ThemeStatus.Text = "Unable to preview theme: " + ex.Message;
        }
    }

    private void OnBackupReadinessClicked(object sender, RoutedEventArgs e)
    {
        var window = new NativeReadinessWindow { Owner = this };
        window.ShowDialog();
    }

    private void OnResetClicked(object sender, RoutedEventArgs e) =>
        ThemeChoice.SelectedValue = NativeThemes.Default.Id;

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (ThemeChoice.SelectedValue is not string id) return;
        try
        {
            NativeThemeSettings.Save(id);
            VaultThemeManager.Apply(id);
            saved = true;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or InvalidOperationException or FormatException)
        {
            ThemeStatus.Text = "Could not save appearance: " + ex.Message +
                ". Your cards and collection are unchanged.";
        }
    }
}
