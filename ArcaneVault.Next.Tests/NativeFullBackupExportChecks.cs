using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class NativeFullBackupExportChecks
{
    private static void Must(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Native full backup export: " + why);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Unsafe native backup export was accepted.");
    }

    private static byte[] Source() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = new object[] { new { name = "Arena Card", qty = 2 } },
        physicalCards = new object[]
        {
            new { name = "Split // Back", qty = 3,
                set = "TST", number = "13", finish = "foil",
                scryfallId = "00000000-0000-4000-8000-000000000788",
                location = "Binder 1" },
            new { name = "Split // Back", qty = 1,
                set = "ALT", number = "012", finish = "etched",
                scryfallId = "", location = "Deckbox 2" }
        },
        deckLibrary = new object[]
        {
            new {
                id = "deck_legacy", name = "Import Me",
                commander = "Test Commander",
                entries = new object[] {
                    new { name = "Island", qty = 7, role = "mana source" }
                },
                revisions = new object[]
                {
                    new { label = "Original", name = "Old deck",
                        commander = "Test Commander",
                        entries = new[] { new { name = "Island", qty = 5 } } }
                },
                customDeckSetting = "preserve"
            }
        },
        preferredArtwork = new { selected = new { uri = "https://example.invalid/art.jpg" } },
        wildcards = new { rare = 2 },
        legacyStorage = new { someUnknownKey = "opaque-value" }
    });

    public static void Run(string work)
    {
        string source = Path.Combine(work, "original-native-export-fixture.json");
        string native = Path.Combine(work, "native-full-backup-active.sqlite");
        string exported = Path.Combine(work, "native-full-backup-exported.json");
        byte[] original = Source();
        File.WriteAllBytes(source, original);
        var sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        var sourceTime = File.GetLastWriteTimeUtc(source);

        var initialized = NativeWorkspace.ImportNewBytes(original, "source-fixture", native);
        var increased = NativeWorkspaceSafety.AddArenaCopies(native, "Arena Card", 3,
            initialized.BackupSha256);
        var deck = NativeWorkspaceSafety.ListDecks(native).Single();
        var updated = NativeWorkspaceSafety.SaveDeck(native,
            "Updated Native Deck", "Test Commander", "8 Island\n1 Sol Ring",
            deck, increased.Current.BackupSha256, format: "Commander");
        var state = NativeWorkspace.Open(native);
        Must(state.Collection.ArenaCopies == 5 &&
             state.Collection.PhysicalCopies == 4 &&
             state.Collection.Decks == 1 &&
             state.Collection.DeckRevisions == 2,
             "Native fixture edits were not persisted before export.");

        byte[] nativeHash = SHA256.HashData(File.ReadAllBytes(native));
        var nativeTime = File.GetLastWriteTimeUtc(native);
        int rollbackCount = NativeWorkspaceSafety.RecoveryPaths(native).Count;

        var output = NativeWorkspaceSafety.ExportFullBackup(native, exported,
            state.BackupSha256);
        Must(File.Exists(exported) &&
             output.BackupSha256 == state.BackupSha256 &&
             output.PhysicalCopies == 4 &&
             output.ArenaCopies == 5 &&
             output.Decks == 1 && output.Revisions == 2 &&
             output.PreferredArtwork == 1 &&
             output.Bytes == new FileInfo(exported).Length,
             "Export did not produce complete native source and manifest summary.");

        var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exported)));
        Must(fileHash == state.BackupSha256,
             "Export must exactly match current native preserved JSON SHA.");
        var identical = NativeCollectionIntegrity.Compare(exported, native);
        Must(identical.ByteIdentical && identical.InventoryMatch &&
             identical.AggregateMatch && identical.TotalDifferingEntries == 0 &&
             identical.Differences.Count == 0 &&
             identical.BackupSha256 == state.BackupSha256 &&
             identical.ActiveSha256 == state.BackupSha256,
             "Same native workspace export must compare byte-identical without showing false deltas.");
        var older = NativeCollectionIntegrity.Compare(source, native);
        Must(!older.ByteIdentical && !older.InventoryMatch &&
             older.BackupArena == 2 && older.ActiveArena == 5 &&
             older.BackupRevisions == 1 && older.ActiveRevisions == 2 &&
             older.TotalDifferingEntries == 1 &&
             older.Differences.Any(x => x.Scope == "Arena" &&
                 x.Item == "Arena Card" &&
                 x.BackupCopies == 2 && x.ActiveCopies == 5) &&
             older.Differences.Any(x => x.Scope == "Physical") == false,
             "Original historical export should show the intended Arena/deck changes, without false printing differences.");
        // Swapping quantities between distinct printings must not be hidden
        // merely because the total number of physical copies is unchanged.
        string redistributed = Path.Combine(work, "integrity-printing-swap.json");
        var swappedJson = JsonNode.Parse(File.ReadAllBytes(exported))!.AsObject();
        var printings = swappedJson["physicalCards"]!.AsArray();
        printings[0]!["qty"] = 2;
        printings[1]!["qty"] = 2;
        File.WriteAllText(redistributed, swappedJson.ToJsonString());
        byte[] sourceBeforeComparison = File.ReadAllBytes(native);
        int recoveriesBeforeComparison = NativeWorkspaceSafety.RecoveryPaths(native).Count;
        var swapped = NativeCollectionIntegrity.Compare(redistributed, native);
        Must(!swapped.ByteIdentical && !swapped.InventoryMatch &&
             swapped.AggregateMatch && swapped.TotalDifferingEntries == 2 &&
             swapped.BackupPhysical == swapped.ActivePhysical &&
             swapped.Differences.Count == 2 &&
             swapped.Differences.All(x => x.Scope == "Physical") &&
             swapped.Differences.Any(x => x.BackupCopies == 2 && x.ActiveCopies == 3) &&
             swapped.Differences.Any(x => x.BackupCopies == 2 && x.ActiveCopies == 1),
             "Equal total copies must not mask changes between individual printings. " + swapped.Display);

        // Metadata-only changes must not be mislabeled as byte-identical,
        // and an inventory match must never imply opaque fields are equal.
        string metadataOnly = Path.Combine(work, "integrity-metadata-only.json");
        var metadataJson = JsonNode.Parse(File.ReadAllBytes(exported))!.AsObject();
        metadataJson["releaseAuditOnly"] = "metadata differs";
        File.WriteAllText(metadataOnly, metadataJson.ToJsonString());
        var metadata = NativeCollectionIntegrity.Compare(metadataOnly, native);
        Must(!metadata.ByteIdentical && metadata.InventoryMatch &&
             metadata.AggregateMatch && metadata.TotalDifferingEntries == 0 &&
             metadata.Status.Contains("backup content differs", StringComparison.Ordinal) &&
             metadata.Display.Contains("does not prove equality", StringComparison.Ordinal),
             "Metadata-only changes must be reported separately from inventory parity.");
        Must(sourceBeforeComparison.SequenceEqual(File.ReadAllBytes(native)) &&
             NativeWorkspaceSafety.RecoveryPaths(native).Count == recoveriesBeforeComparison,
             "Read-only comparison changed the native workspace or recovery history.");

        var fakeBad = Path.Combine(work, "integrity-invalid.json");
        File.WriteAllText(fakeBad, "{\"schema\":99}");
        Reject(() => NativeCollectionIntegrity.Compare(fakeBad, native));
        Reject(() => NativeCollectionIntegrity.Compare(native, native));
        var importedView = NativeBackupReader.Read(exported);
        Must(importedView.PhysicalCards.Count == 2 &&
             importedView.ArenaCards.Single().Quantity == 5 &&
             importedView.Decks == 1 &&
             importedView.DeckRevisions == 2 &&
             importedView.PreferredArtworks == 1,
             "Export does not contain current native ownership, decks and revisions.");

        using (var doc = JsonDocument.Parse(File.ReadAllBytes(exported)))
        {
            var root = doc.RootElement;
            var decks = root.GetProperty("deckLibrary");
            Must(decks[0].GetProperty("name").GetString() == "Updated Native Deck" &&
                 decks[0].GetProperty("entries")[0].GetProperty("qty").GetInt32() == 8 &&
                 decks[0].GetProperty("entries")[0].GetProperty("role").GetString() == "mana source" &&
                 decks[0].GetProperty("format").GetString() == "Commander" &&
                 decks[0].GetProperty("revisions")[0].GetProperty("label").GetString() == "Original" &&
                 decks[0].GetProperty("customDeckSetting").GetString() == "preserve" &&
                 root.GetProperty("physicalCards")[0].GetProperty("location").GetString() ==
                     "Binder 1" &&
                 root.GetProperty("physicalCards")[1].GetProperty("finish").GetString() ==
                     "etched" &&
                 root.GetProperty("legacyStorage").GetProperty("someUnknownKey").GetString() ==
                     "opaque-value" &&
                 root.GetProperty("wildcards").GetProperty("rare").GetInt32() == 2,
                 "Export lost legacy settings, physical printings or native deck edits.");
        }

        // A real full exported backup must be importable, even if the original
        // Windows/WebView profile has been uninstalled.
        string replay = Path.Combine(work, "native-full-backup-reimport.sqlite");
        var restored = NativeWorkspace.ImportNew(exported, replay);
        Must(restored.Collection.PhysicalCards.SequenceEqual(state.Collection.PhysicalCards) &&
             restored.Collection.ArenaCards.SequenceEqual(state.Collection.ArenaCards) &&
             restored.Collection.DeckRevisions == state.Collection.DeckRevisions &&
             restored.BackupSha256 == state.BackupSha256,
             "Native-exported backup cannot bootstrap an identical independent workspace.");

        var backupHash = SHA256.HashData(File.ReadAllBytes(exported));
        var backupTime = File.GetLastWriteTimeUtc(exported);
        Reject(() => NativeWorkspaceSafety.ExportFullBackup(native, exported,
            state.BackupSha256));
        Reject(() => NativeWorkspaceSafety.ExportFullBackup(native,
            Path.Combine(work, "native-full-backup-not-json.dat"), state.BackupSha256));
        Reject(() => NativeWorkspaceSafety.ExportFullBackup(native,
            Path.Combine(work, "native-full-backup-stale.json"), initialized.BackupSha256));
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            Reject(() => NativeWorkspaceSafety.ExportFullBackup(native,
                Path.Combine(work, "native-full-backup-canceled.json"),
                state.BackupSha256, cancel.Token));
        }

        Must(backupHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(exported))) &&
             backupTime == File.GetLastWriteTimeUtc(exported) &&
             !File.Exists(Path.Combine(work, "native-full-backup-canceled.json")) &&
             !File.Exists(Path.Combine(work, "native-full-backup-stale.json")) &&
             Directory.GetFiles(work, "*.partial").Length == 0 &&
             rollbackCount == NativeWorkspaceSafety.RecoveryPaths(native).Count &&
             nativeHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(native))) &&
             nativeTime == File.GetLastWriteTimeUtc(native) &&
             sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))) &&
             sourceTime == File.GetLastWriteTimeUtc(source),
             "Rejected export damaged source/target, made a new rollback or left a partial file.");

        Console.WriteLine("PASS: native full backup export includes recent card/deck edits, revisions, all unknown fields; portable reimport, no overwrite, stale/canceled guard, unchanged SQLite");
    }
}
