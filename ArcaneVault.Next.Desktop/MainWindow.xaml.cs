using Microsoft.Win32;
using ArcaneVault.Next.EDHLab;
using ArcaneVault.Next.Domain;
using ArcaneVault.Next.Collection;
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
    private CommanderBrowserWindow? edhLabWindow;
    private NativeCollectionWindow? collectionWindow;
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
        Loaded += async (_, _) => await InitializeNativeAsync();
        Closed += (_, _) =>
        {
            closing = true;
            edhLabWindow?.Close();
            collectionWindow?.Close();
            CancelPendingSearch();
            catalogSnapshot?.Dispose();
        };
    }

    /// <summary>
    /// Start from the independent native profile. Never read or modify the
    /// installed WebView app automatically and never silently import a backup.
    /// A corrupt workspace is reported, not reset.
    /// </summary>
    private async Task InitializeNativeAsync()
    {
        OpenNativeButton.IsEnabled = System.IO.File.Exists(NativeWorkspace.DefaultPath);
        ImportNativeButton.IsEnabled = !OpenNativeButton.IsEnabled;
        MigrateLegacyButton.IsEnabled = true;
        SaveNativeCatalogButton.IsEnabled = false;

        if (System.IO.File.Exists(NativeWorkspace.CatalogPath))
        {
            try
            {
                var persisted = await Task.Run(() =>
                    CatalogSnapshot.OpenPersistent(NativeWorkspace.CatalogPath));
                if (closing) { persisted.Dispose(); return; }
                var previous = catalogSnapshot;
                catalogSnapshot = persisted;
                modeVersion++;
                previous?.Dispose();
                ModeStatus.Text =
                    $"{persisted.TotalPrintings:N0} printings loaded from private native catalog · No live data edits";
                _ = FilterAsync();
            }
            catch (Exception)
            {
                if (!closing)
                    ModeStatus.Text =
                        "Native catalog could not be loaded. Choose a catalog source manually; no files were altered.";
            }
        }

        if (!closing && System.IO.File.Exists(NativeWorkspace.DefaultPath))
            await OpenNativeCollectionAsync();
    }

    private async void OnSaveNativeCatalogClicked(object sender, RoutedEventArgs e)
    {
        var active = catalogSnapshot;
        if (active is null) return;
        if (System.IO.File.Exists(NativeWorkspace.CatalogPath))
        {
            FilterStatus.Text = "A native catalog already exists. Existing data was preserved.";
            return;
        }

        SaveNativeCatalogButton.IsEnabled = false;
        OpenCatalogButton.IsEnabled = false;
        SyntheticButton.IsEnabled = false;
        FilterStatus.Text = "Saving a native catalog once, without changing the installed app…";
        int version = modeVersion;
        try
        {
            System.IO.Directory.CreateDirectory(NativeWorkspace.RootDirectory);
            var saved = await Task.Run(() =>
                CatalogSnapshot.SavePersistentCopy(active.ReadOnlySnapshotPath,
                    NativeWorkspace.CatalogPath));
            if (closing || version != modeVersion)
            {
                saved.Dispose();
                return;
            }
            CancelPendingSearch();
            collectionWindow?.Close();
            collectionWindow = null;
            var previous = catalogSnapshot;
            catalogSnapshot = saved;
            modeVersion++;
            previous?.Dispose();
            ModeStatus.Text =
                $"{saved.TotalPrintings:N0} printings now loaded directly from the native catalog";
            FilterStatus.Text = "Native catalog saved. It will reopen automatically next launch.";
            _ = FilterAsync();
        }
        catch (Exception)
        {
            if (!closing) FilterStatus.Text =
                "Native catalog import failed or already exists. Original catalog was not changed.";
        }
        finally
        {
            if (!closing)
            {
                SaveNativeCatalogButton.IsEnabled =
                    catalogSnapshot is not null &&
                    !System.IO.File.Exists(NativeWorkspace.CatalogPath);
                OpenCatalogButton.IsEnabled = true;
                SyntheticButton.IsEnabled = true;
            }
        }
    }

    private async void OnMigrateLegacyClicked(object sender, RoutedEventArgs e)
    {
        bool compareOnly = System.IO.File.Exists(NativeWorkspace.DefaultPath);
        if (!System.IO.Directory.Exists(LegacyProfileSnapshot.LiveProfilePath))
        {
            FilterStatus.Text = "No installed Arcane Vault browser profile was found. Use Import backup once if necessary.";
            return;
        }

        if (MessageBox.Show(this,
            "Close the old Arcane Vault Windows app before continuing.\n\n" +
            "This reads a temporary ISOLATED COPY of its local browser storage. " +
            "The original app and collection will not be modified, and the new native workspace is read-only. " +
            "You will review recovered totals before anything could be imported. " +
            "If a native workspace already exists, this is COMPARISON ONLY and cannot replace it.\n\n" +
            "This one-time migration requires the installed Microsoft Edge WebView2 Runtime. Continue?",
            "Migrate installed Arcane Vault collection", MessageBoxButton.YesNo,
            MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;

        MigrateLegacyButton.IsEnabled = false;
        ImportNativeButton.IsEnabled = false;
        OpenNativeButton.IsEnabled = false;
        FilterStatus.Text = "Reading an isolated snapshot of installed Arcane Vault WebView storage…";
        try
        {
            var recovered = await OfflineLegacyBridge.ExtractAsync(this);
            if (closing) return;

            var view = recovered.Collection;
            if (compareOnly)
            {
                var existing = await Task.Run(() =>
                    NativeWorkspace.Open(NativeWorkspace.DefaultPath));
                if (closing) return;
                bool physicalMatch = existing.Collection.PhysicalCards.SequenceEqual(view.PhysicalCards);
                bool arenaMatch = existing.Collection.ArenaCards.SequenceEqual(view.ArenaCards);
                string match(bool yes) => yes ? "MATCH" : "DIFFERENT";
                MessageBox.Show(this,
                    $"Installed legacy profile (read-only):\n" +
                    $"  Physical {view.PhysicalCopies:N0} copies / {view.PhysicalCards.Count:N0} rows\n" +
                    $"  Arena {view.ArenaCopies:N0} copies / {view.ArenaCards.Count:N0} entries\n" +
                    $"  Decks {view.Decks:N0}, revisions {view.DeckRevisions:N0}, art {view.PreferredArtworks:N0}\n\n" +
                    $"Existing native workspace:\n" +
                    $"  Physical {existing.Collection.PhysicalCopies:N0} copies / " +
                      $"{existing.Collection.PhysicalCards.Count:N0} rows — {match(physicalMatch)}\n" +
                    $"  Arena {existing.Collection.ArenaCopies:N0} copies / " +
                      $"{existing.Collection.ArenaCards.Count:N0} entries — {match(arenaMatch)}\n" +
                    $"  Deck counts: {existing.Collection.Decks:N0} — " +
                      $"{match(existing.Collection.Decks == view.Decks)}\n" +
                    $"  Revision counts: {existing.Collection.DeckRevisions:N0} — " +
                      $"{match(existing.Collection.DeckRevisions == view.DeckRevisions)}\n" +
                    $"  Artwork counts: {existing.Collection.PreferredArtworks:N0} — " +
                      $"{match(existing.Collection.PreferredArtworks == view.PreferredArtworks)}\n\n" +
                    "Physical/Arena MATCH compares full typed inventory rows, including printing IDs. " +
                    "Deck/art comparison checks counts only. Nothing was changed or overwritten.",
                    "Installed vs native collection — read-only comparison",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                FilterStatus.Text = "Read-only legacy comparison finished. Native database is unchanged.";
                return;
            }

            if (MessageBox.Show(this,
                $"Legacy source recovered:\n" +
                $"Physical: {view.PhysicalCards.Count:N0} inventory rows / {view.PhysicalCopies:N0} copies\n" +
                $"Arena: {view.ArenaCards.Count:N0} entries / {view.ArenaCopies:N0} copies\n" +
                $"Decks: {view.Decks:N0}, revisions: {view.DeckRevisions:N0}\n" +
                $"Preferred artwork: {view.PreferredArtworks:N0}\n" +
                $"Missing or invalid physical printing IDs: {view.CopiesWithMissingOrInvalidIds:N0} copies\n\n" +
                "Create a NEW private native workspace with these exact records? " +
                "The installed app will remain unchanged. Empty or unexpected totals should be investigated before confirming.",
                "Verify local collection before native migration", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                FilterStatus.Text = "Migration canceled. No native workspace was created.";
                return;
            }

            FilterStatus.Text = "Verifying and committing independent native collection…";
            System.IO.Directory.CreateDirectory(NativeWorkspace.RootDirectory);
            await Task.Run(() => NativeWorkspace.ImportNewBytes(
                recovered.BackupBytes, "installed-webview2-offline-snapshot",
                NativeWorkspace.DefaultPath));
            if (closing) return;

            // Catalog is derived metadata, never authoritative ownership.
            // Its import is optional and must not invalidate the new collection.
            string legacyCatalog = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArcaneVault", "scryfall-catalog.sqlite");
            if (System.IO.File.Exists(legacyCatalog) &&
                !System.IO.File.Exists(NativeWorkspace.CatalogPath))
            {
                try
                {
                    using var nativeCatalog = await Task.Run(() =>
                        CatalogSnapshot.SavePersistentCopy(
                            legacyCatalog, NativeWorkspace.CatalogPath));
                }
                catch (Exception)
                {
                    if (!closing) FilterStatus.Text =
                        "Collection migrated; optional native catalog copy failed. Card metadata can be loaded separately.";
                }
            }

            if (closing) return;
            if (System.IO.File.Exists(NativeWorkspace.CatalogPath))
            {
                try
                {
                    var catalog = await Task.Run(() =>
                        CatalogSnapshot.OpenPersistent(NativeWorkspace.CatalogPath));
                    if (closing) { catalog.Dispose(); return; }
                    collectionWindow?.Close();
                    collectionWindow = null;
                    CancelPendingSearch();
                    var previous = catalogSnapshot;
                    catalogSnapshot = catalog;
                    modeVersion++;
                    previous?.Dispose();
                    _ = FilterAsync();
                }
                catch (Exception) { /* Collection still safe and independently readable. */ }
            }

            FilterStatus.Text = "Direct native migration completed. Reopening persisted collection…";
            await OpenNativeCollectionAsync();
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                FilterStatus.Text = "Direct migration did not complete. Existing data was not overwritten.";
                MessageBox.Show(this,
                    "Migration was stopped without changing the installed app.\n\n" + ex.Message +
                    "\n\nIf the original app uses an unsupported browser profile, " +
                    "the previous manual full-backup import is still available.",
                    "Arcane Vault Next — safe migration stop",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (!closing)
            {
                bool exists = System.IO.File.Exists(NativeWorkspace.DefaultPath);
                OpenNativeButton.IsEnabled = exists;
                ImportNativeButton.IsEnabled = !exists;
                MigrateLegacyButton.IsEnabled = true;
            }
        }
    }

    private async void OnImportNativeClicked(object sender, RoutedEventArgs e)
    {
        if (System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Native collection already exists. Import is blocked to prevent replacement.";
            return;
        }
        var picker = new OpenFileDialog
        {
            Title = "Import a complete full backup once into a native Windows workspace",
            Filter = "Arcane Vault full backup (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog(this) != true) return;

        if (MessageBox.Show(this,
            "This will make an independent, persistent, READ-ONLY native SQLite collection from your exported full backup. " +
            "It will not alter the installed Arcane Vault app, and it will NOT silently replace an existing native workspace. Continue?",
            "Import once into native Arcane Vault Next", MessageBoxButton.YesNo,
            MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;

        ImportNativeButton.IsEnabled = false;
        OpenNativeButton.IsEnabled = false;
        FilterStatus.Text = "Importing and validating complete backup into a native workspace…";
        try
        {
            System.IO.Directory.CreateDirectory(NativeWorkspace.RootDirectory);
            await Task.Run(() => NativeWorkspace.ImportNew(
                picker.FileName, NativeWorkspace.DefaultPath));
            if (closing) return;
            FilterStatus.Text = "Native collection imported and verified. Reopening persisted data…";
            await OpenNativeCollectionAsync();
        }
        catch (Exception)
        {
            if (!closing) FilterStatus.Text =
                "Native import failed or workspace already exists; original backup and installed app are unchanged.";
        }
        finally
        {
            if (!closing)
            {
                OpenNativeButton.IsEnabled =
                    System.IO.File.Exists(NativeWorkspace.DefaultPath);
                ImportNativeButton.IsEnabled = !OpenNativeButton.IsEnabled;
                MigrateLegacyButton.IsEnabled = true;
            }
        }
    }

    private async void OnOpenNativeClicked(object sender, RoutedEventArgs e) =>
        await OpenNativeCollectionAsync();

    private async Task OpenNativeCollectionAsync()
    {
        if (collectionWindow is not null)
        {
            collectionWindow.Activate();
            return;
        }
        if (!System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "No native collection yet. Choose Import backup once.";
            return;
        }
        var activeCatalog = catalogSnapshot;
        int version = modeVersion;
        OpenNativeButton.IsEnabled = false;
        FilterStatus.Text = "Verifying native SQLite workspace and loading local collection…";
        try
        {
            var bundle = await Task.Run(() =>
            {
                var workspace = NativeWorkspace.Open(NativeWorkspace.DefaultPath);
                var verification = CollectionCatalogReview.Examine(
                    workspace.Collection, activeCatalog?.ReadOnlySnapshotPath);
                var progress = activeCatalog is null ? null :
                    NativeSetProgress.Build(verification, activeCatalog.ReadOnlySnapshotPath);
                return (workspace, verification, progress);
            });
            if (closing || version != modeVersion ||
                !ReferenceEquals(activeCatalog, catalogSnapshot)) return;

            var view = new NativeCollectionWindow(bundle.workspace.Collection,
                bundle.verification, activeCatalog?.ReadOnlySnapshotPath,
                bundle.progress, nativeWorkspace: true)
            { Owner = this };
            collectionWindow = view;
            view.Closed += (_, _) =>
            {
                if (ReferenceEquals(collectionWindow, view))
                    collectionWindow = null;
            };
            view.Show();
            FilterStatus.Text =
                $"Native collection: {bundle.workspace.Collection.PhysicalCopies:N0} physical + " +
                $"{bundle.workspace.Collection.ArenaCopies:N0} Arena copies · restored from local SQLite";
            ModeStatus.Text = $"Private native workspace: {bundle.workspace.OriginalFileName} · " +
                $"imported {bundle.workspace.ImportedAtUtc} · read-only / source preserved";
        }
        catch (Exception)
        {
            if (!closing)
                FilterStatus.Text =
                    "Native workspace verification failed. Data was NOT reset; preserve the file and use backup preview.";
        }
        finally
        {
            if (!closing)
            {
                OpenNativeButton.IsEnabled =
                    System.IO.File.Exists(NativeWorkspace.DefaultPath);
                ImportNativeButton.IsEnabled = !OpenNativeButton.IsEnabled;
                MigrateLegacyButton.IsEnabled = true;
            }
        }
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
            edhLabWindow?.Close();
            collectionWindow?.Close();
            collectionWindow = null;
            var previous = catalogSnapshot;
            catalogSnapshot = loaded;
            EdhLabButton.IsEnabled = true;
            SaveNativeCatalogButton.IsEnabled =
                !System.IO.File.Exists(NativeWorkspace.CatalogPath);
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

    private async void OnCollectionBackupClicked(object sender, RoutedEventArgs e)
    {
        if (collectionWindow is not null)
        {
            collectionWindow.Activate();
            return;
        }

        var picker = new OpenFileDialog
        {
            Title = "Select an exported full Windows backup (read-only)",
            Filter = "Arcane Vault backup (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog(this) != true) return;

        CollectionBackupButton.IsEnabled = false;
        OpenCatalogButton.IsEnabled = false;
        SyntheticButton.IsEnabled = false;
        FilterStatus.Text = "Reading backup and checking trusted printing IDs…";
        var activeCatalog = catalogSnapshot;
        var version = modeVersion;
        try
        {
            var result = await Task.Run(() =>
            {
                var view = NativeBackupReader.Read(picker.FileName);
                var verification = CollectionCatalogReview.Examine(
                    view, activeCatalog?.ReadOnlySnapshotPath);
                var setProgress = activeCatalog is null ? null :
                    NativeSetProgress.Build(verification, activeCatalog.ReadOnlySnapshotPath);
                return (view, verification, setProgress);
            });
            if (closing || version != modeVersion ||
                !ReferenceEquals(activeCatalog, catalogSnapshot)) return;
            var dialog = new NativeCollectionWindow(
                result.view, result.verification,
                activeCatalog?.ReadOnlySnapshotPath, result.setProgress)
            { Owner = this };
            collectionWindow = dialog;
            dialog.Closed += (_, _) =>
            {
                if (ReferenceEquals(collectionWindow, dialog)) collectionWindow = null;
            };
            dialog.Show();
            FilterStatus.Text = result.verification.CatalogVerified
                ? $"Verified {result.verification.ConfirmedCopies:N0} physical copies · " +
                  $"{result.verification.UnresolvedCopies:N0} unresolved (read-only)"
                : $"Backup: {result.view.PhysicalCopies:N0} physical, " +
                  $"{result.view.ArenaCopies:N0} Arena copies · catalog not loaded";
        }
        catch (Exception)
        {
            if (!closing)
                FilterStatus.Text =
                    "Backup preview failed or file unsupported. Existing collection remains untouched.";
        }
        finally
        {
            if (!closing)
            {
                CollectionBackupButton.IsEnabled = true;
                OpenCatalogButton.IsEnabled = true;
                SyntheticButton.IsEnabled = true;
            }
        }
    }

    private async void OnEdhLabClicked(object sender, RoutedEventArgs e)
    {
        var active = catalogSnapshot;
        if (active is null) return;
        if (edhLabWindow is not null)
        {
            edhLabWindow.Activate();
            return;
        }

        var currentVersion = modeVersion;
        EdhLabButton.IsEnabled = false;
        FilterStatus.Text = "Opening native EDH Lab from isolated catalog…";
        try
        {
            var index = await Task.Run(() =>
                CommanderCatalog.OpenReadOnly(active.ReadOnlySnapshotPath));
            if (closing || currentVersion != modeVersion ||
                !ReferenceEquals(active, catalogSnapshot)) return;
            var window = new CommanderBrowserWindow(index) { Owner = this };
            edhLabWindow = window;
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(edhLabWindow, window)) edhLabWindow = null;
                if (!closing && catalogSnapshot is not null) EdhLabButton.IsEnabled = true;
            };
            window.Show();
            FilterStatus.Text = $"{index.LoadedCardIdentities:N0} read-only commander candidates loaded";
        }
        catch (Exception)
        {
            if (!closing)
                FilterStatus.Text = "EDH Lab needs a fully indexed Scryfall card catalog. Current collection data is unchanged.";
        }
        finally
        {
            if (!closing && edhLabWindow is null)
                EdhLabButton.IsEnabled = catalogSnapshot is not null;
        }
    }

    private void OnSyntheticClicked(object sender, RoutedEventArgs e)
    {
        CancelPendingSearch();
        edhLabWindow?.Close();
        collectionWindow?.Close();
        collectionWindow = null;
        EdhLabButton.IsEnabled = false;
        SaveNativeCatalogButton.IsEnabled = false;
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
