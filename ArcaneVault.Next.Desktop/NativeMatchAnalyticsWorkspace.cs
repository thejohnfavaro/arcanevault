using Microsoft.Data.Sqlite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>Dedicated read-only analytics workspace, separate from match corrections.</summary>
internal sealed class NativeMatchAnalyticsWorkspace : UserControl
{
    private readonly NativeMatchPerformancePanel dashboard = new();
    private readonly TextBlock status = new() { Foreground = Brushes.LightGray,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,12) };
    private readonly Button refresh = new() { Content = "Refresh match analytics",
        Padding = new Thickness(12,8,12,8), HorizontalAlignment = HorizontalAlignment.Left };
    private bool reading;
    internal NativeMatchAnalyticsWorkspace()
    {
        var root = new DockPanel { Margin = new Thickness(24,18,24,18) };
        Content = root;
        var header = new StackPanel();
        DockPanel.SetDock(header,Dock.Top);
        root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "DECK ANALYTICS",
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 27 });
        header.Children.Add(new TextBlock {
            Text = "Confirmed Arena performance by deck, queue and date. " +
                   "Comparisons use recorded outcomes only; corrections belong in Match History.",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,5,0,8) });
        header.Children.Add(refresh);
        header.Children.Add(status);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.Content = dashboard;
        root.Children.Add(scroll);
        refresh.Click += async (_,_) => await ReloadAsync();
        Loaded += async (_,_) => await ReloadAsync();
    }
    internal async Task ReloadAsync()
    {
        if (reading) return;
        reading=true;
        refresh.IsEnabled=false;
        status.Text="Reading locally recorded Arena matches…";
        try
        {
            var matches=await Task.Run(NativeMatchHistoryView.ReadPerformance);
            dashboard.SetMatches(matches);
            status.Text=$"{matches.Count} distinct match row(s) available for review. " +
                "All calculations exclude unassigned rows and unconfirmed outcomes.";
        }
        catch(Exception ex) when (ex is IOException or SqliteException or
            UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            status.Text="Analytics unavailable: "+ex.Message+
                ". Your match data has not been modified.";
        }
        finally { reading=false; refresh.IsEnabled=true; }
    }
}
