using ArcaneVault.Next.Migration;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class OfflineStagingChecks
{
    static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Offline staging: " + message);
    }

    static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid stage accepted.");
    }

    public static void Run(string work)
    {
        string backup = Path.Combine(work, "offline-source.json");
        string output = Path.Combine(work, "offline-staged.sqlite");
        var source = new
        {
            schema = 2,
            cards = new[] { new { name = "Arena Card", qty = 3 }, new { name = "Zero Owned", qty = 0 } },
            physicalCards = new object[] {
                new { name = "Split // Back", qty = 2, set = "AAA", number = "5", finish = "foil",
                    scryfallId = "00000000-0000-4000-8000-000000000101" },
                new { name = "Split // Back", qty = 4, set = "BBB", number = "7", finish = "nonfoil",
                    scryfallId = "" },
                new { name = "Unknown Printing", qty = 1, set = "", number = "",
                    finish = "etched", scryfallId = "" }
            },
            deckLibrary = new[] {
                new { name = "Sample Commander", entries = new[] { new { name = "Split // Back", qty = 2 } },
                    revisions = new[] { new { name = "Old", entries = Array.Empty<object>() } } }
            },
            preferredArtwork = new { card = new { image = "https://example.invalid/image" } },
            wildcards = new { rare = 3 },
            settings = new { theme = "dark" }
        };
        File.WriteAllText(backup, JsonSerializer.Serialize(source));
        var originalHash = SHA256.HashData(File.ReadAllBytes(backup));
        var originalDate = File.GetLastWriteTimeUtc(backup);
        var observed = OfflineStaging.Create(backup, output);
        Assert(observed.Verified && !observed.InstalledDataModified, "Safety flags incorrect.");
        Assert(observed.ArenaRows == 2 && observed.ArenaCopies == 3 &&
            observed.PhysicalRows == 3 && observed.PhysicalCopies == 7 &&
            observed.Decks == 1 && observed.DeckRevisions == 1 &&
            observed.ArtworkPreferences == 1 && observed.ValidPrintIdRows == 1, "Count parity failed.");
        Assert(observed == OfflineStaging.Verify(output), "Stage verification differed.");
        Assert(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(backup))) &&
            originalDate == File.GetLastWriteTimeUtc(backup), "Source was modified.");

        var interrupted = Path.Combine(work, "offline-interrupt.sqlite");
        bool thrown = false;
        try
        {
            OfflineStaging.CreateCore(backup, interrupted, count =>
            {
                if (count == 3) throw new IOException("Injected interruption");
            });
        }
        catch (IOException) { thrown = true; }
        Assert(thrown && !File.Exists(interrupted), "Interrupted stage created a visible output.");
        Assert(Directory.GetFiles(work, "offline-interrupt.sqlite.*.partial").Length == 0,
            "Interrupted stage leaked a partial database.");
        Assert(observed == OfflineStaging.Verify(output), "Interrupted stage altered earlier output.");

        string existing = Path.Combine(work, "offline-existing.sqlite");
        File.WriteAllText(existing, "Do not overwrite");
        Reject(() => OfflineStaging.Create(backup, existing));
        Assert(File.ReadAllText(existing) == "Do not overwrite", "Existing output was overwritten.");

        string badBackup = Path.Combine(work, "offline-invalid.json");
        File.WriteAllText(badBackup, "{\"schema\":2,\"cards\":[],\"physicalCards\":[{\"name\":\"Bad\",\"qty\":-1}],\"deckLibrary\":[],\"preferredArtwork\":{}}");
        var badOutput = Path.Combine(work, "offline-failed.sqlite");
        Reject(() => OfflineStaging.Create(badBackup, badOutput));
        Assert(!File.Exists(badOutput), "Invalid source produced a completed stage.");

        var sync = Path.Combine(work, "offline-qr.json");
        File.WriteAllText(sync, JsonSerializer.Serialize(new {
            format = "arcane-vault-collection-sync", schema = 1,
            arenaCards = Array.Empty<object>(), physicalCards = Array.Empty<object>()
        }));
        Reject(() => OfflineStaging.Create(sync, Path.Combine(work, "offline-qr.sqlite")));

        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = output, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE stage_records SET qty=999 WHERE section='physical' AND ordinal=0";
            cmd.ExecuteNonQuery();
        }
        Reject(() => OfflineStaging.Verify(output));
        Assert(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(backup))),
            "Corruption test touched original backup.");

        Console.WriteLine("PASS: isolated transactional staging; 7 physical copies; exact readback; no overwrite; interruption rollback; tamper detection");
    }
}
