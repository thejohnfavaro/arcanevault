using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Native WPF paging and query performance spike. Synthetic by default; users
/// must explicitly choose a Scryfall SQLite catalog to make a read-only snapshot.
/// No ownership or deck data is imported or modified.
/// </summary>
public partial class MainWindow : Window
{
    private sealed record SampleCard(string Name, string SetCode, string CollectorNumber);

    private readonly IReadOnlyList<SampleCard> sampleCards;
    private CatalogSnapshot? catalogSnapshot;
    private CancellationTokenSource? searchCancellation;
    private bool closing;
    private int modeVersion;

    public MainWindow()
    {
        InitializeComponent();
        sampleCards = Enumerable.Range(1, 25_000).Select(i =>
            new SampleCard($"Sample Card {i:D5}", $"S{i % 60:D2}", $"#{i % 450 + 1:D3}")).ToArray();
        CardsList.ItemsSource = sampleCards;
        FilterStatus.Text = $"{sampleCards.Count:N0} synthetic cards";
        Closed += (_, _) =>
        {
            closing = true;
            CancelPendingSearch();
            catalogSnapshot?.Dispose();
        };
    }

    private void CancelPendingSearch()
    {
        var old = searchCancellation;
        searchCancellation = null;
        if (old is null) return;
        try { old.Cancel(); } catch (ObjectDisposedException) { }
        // The owning async query will dispose its own token in finally.
    }

    private async void OnOpenCatalogClicked(object sender, RoutedEventArgs e)
    {
        var expected = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArcaneVault", "scryfall-catalog.sqlite");
        var picker = new OpenFileDialog
        {
            Title = "Select a Scryfall catalog to snapshot (read-only)",
            Filter = "SQLite catalogs (*.sqlite;*.sqlite3;*.db)|*.sqlite;*.sqlite3;*.db|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(expected))
                ? System.IO.Path.GetDirectoryName(expected)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileName = System.IO.File.Exists(expected) ? "scryfall-catalog.sqlite" : ""
        };
        if (picker.ShowDialog(this) != true) return;

        OpenCatalogButton.IsEnabled = false;
        SyntheticButton.IsEnabled = false;
        FilterStatus.Text = "Copying SQLite snapshot…";
        try
        {
            // SQLite's online backup API makes a consistent temporary copy,
            // even when the installed app uses a WAL journal.
            var loaded = await Task.Run(() => CatalogSnapshot.Open(picker.FileName));
            if (closing) { loaded.Dispose(); return; }
            CancelPendingSearch();
            var previous = catalogSnapshot;
            catalogSnapshot = loaded;
            modeVersion++;
            previous?.Dispose();
            ModeStatus.Text = $"{loaded.TotalPrintings:N0} catalog printings in isolated snapshot · Prefix search · First 200 rows only · No live edits";
            SearchBox.Text = "";
            _ = FilterAsync();
        }
        catch (Exception)
        {
            if (!closing)
            {
                FilterStatus.Text = "Could not open a valid Scryfall catalog snapshot";
                ModeStatus.Text = "The existing sample or snapshot is unchanged. Check that you selected scryfall-catalog.sqlite.";
            }
        }
        finally
        {
            if (!closing)
            {
                OpenCatalogButton.IsEnabled = true;
                SyntheticButton.IsEnabled = true;
            }
        }
    }

    private void OnSyntheticClicked(object sender, RoutedEventArgs e)
    {
        CancelPendingSearch();
        modeVersion++;
        var previous = catalogSnapshot;
        catalogSnapshot = null;
        previous?.Dispose();
        ModeStatus.Text = "Synthetic mode · No catalog file open. Nothing is imported, saved, or synced.";
        SearchBox.Text = "";
        _ = FilterAsync();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (sampleCards is null) return; // can fire inside InitializeComponent
        await FilterAsync();
    }

    private async Task FilterAsync()
    {
        CancelPendingSearch();
        var cts = new CancellationTokenSource();
        searchCancellation = cts;
        var token = cts.Token;
        var filter = SearchBox.Text.Trim();
        var active = catalogSnapshot;
        var version = modeVersion;
        try
        {
            await Task.Delay(140, token);
            if (active is null)
            {
                var started = Stopwatch.GetTimestamp();
                var list = await Task.Run(() => sampleCards.Where(c =>
                    c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.SetCode.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.CollectorNumber.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToArray(), token);
                token.ThrowIfCancellationRequested();
                if (closing || version != modeVersion || !ReferenceEquals(searchCancellation, cts)) return;
                CardsList.ItemsSource = list;
                FilterStatus.Text = $"{list.Length:N0} synthetic rows · {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms";
            }
            else
            {
                var results = await Task.Run(() => active.SearchPrefix(filter, cancellationToken: token), token);
                token.ThrowIfCancellationRequested();
                if (closing || version != modeVersion || !ReferenceEquals(searchCancellation, cts)) return;
                CardsList.ItemsSource = results.Rows;
                FilterStatus.Text = $"{results.Rows.Count:N0} printings · SQL {results.SqlMilliseconds:F1} ms" +
                    (results.MayHaveMore ? " · First 200" : "");
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { } // old snapshot closed after switching modes
        catch (Exception)
        {
            if (!closing && version == modeVersion && ReferenceEquals(searchCancellation, cts))
                FilterStatus.Text = "Catalog query failed; existing snapshot was not modified";
        }
        finally
        {
            if (ReferenceEquals(searchCancellation, cts)) searchCancellation = null;
            cts.Dispose();
        }
    }
}
