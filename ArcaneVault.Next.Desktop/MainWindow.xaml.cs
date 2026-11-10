using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Native WPF virtualization/async filtering spike. All data are deterministic
/// synthetic items. This window intentionally never opens real Arcane Vault data.
/// </summary>
public partial class MainWindow : Window
{
    private sealed record SampleCard(string Name, string SetCode, string CollectorNumber);

    private readonly IReadOnlyList<SampleCard> catalog;
    private CancellationTokenSource? filterCancellation;

    public MainWindow()
    {
        InitializeComponent();
        catalog = Enumerable.Range(1, 25_000).Select(i =>
            new SampleCard($"Sample Card {i:D5}", $"S{i % 60:D2}", $"#{i % 450 + 1:D3}")).ToArray();

        CardsList.ItemsSource = catalog;
        FilterStatus.Text = $"{catalog.Count:N0} synthetic rows";
        Closed += (_, _) => { filterCancellation?.Cancel(); filterCancellation?.Dispose(); };
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (catalog is null) return; // TextChanged may fire during InitializeComponent.
        filterCancellation?.Cancel();
        filterCancellation?.Dispose();
        using var cancellation = new CancellationTokenSource();
        filterCancellation = cancellation;
        var token = cancellation.Token;
        var term = SearchBox.Text.Trim();
        try
        {
            // Avoid unnecessary re-query and repaint while someone is typing.
            await Task.Delay(140, token);
            var started = Stopwatch.GetTimestamp();
            var filtered = await Task.Run(() => catalog.Where(c =>
                c.Name.Contains(term, System.StringComparison.OrdinalIgnoreCase) ||
                c.SetCode.Contains(term, System.StringComparison.OrdinalIgnoreCase) ||
                c.CollectorNumber.Contains(term, System.StringComparison.OrdinalIgnoreCase))
                .ToArray(), token);
            token.ThrowIfCancellationRequested();
            CardsList.ItemsSource = filtered; // UI thread; list containers are virtualized.
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            FilterStatus.Text = $"{filtered.Length:N0} rows · filter {elapsed:F1} ms (sample)";
        }
        catch (OperationCanceledException) { }
    }
}
