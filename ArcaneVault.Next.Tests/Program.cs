using ArcaneVault.Next.Desktop;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Security.Cryptography;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static string Hash(string path) =>
    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

// Named, discoverable test groups. Default invocation still runs every group.
var suiteDescriptions = new (string Id, string Description)[] {
    ("catalog-snapshot", "Synthetic SQLite read-only search and snapshot integrity"),
    ("match-assignment", "Match results attach to the correct saved deck"),
    ("match-performance", "Win rates and match analytics exclude ambiguous results"),
    ("match-deck-attribution", "Arena matches link to verified deck identities"),
    ("arena-log-assignment", "Arena logs record matches and gameplay evidence safely"),
    ("arena-queue-rank", "Queue and rank data is interpreted correctly"),
    ("arena-rank-season", "Ranked seasons and resets are read correctly"),
    ("ownership-golden", "Owned cards reconcile against the reference collection"),
    ("migration-preview", "Legacy migration previews do not mutate live records"),
    ("offline-staging", "Imported data is safely staged"),
    ("native-backup-reader", "Backups can be read and validated"),
    ("native-large-collection", "Large collections remain supported"),
    ("native-workspace", "Local collection storage behaves correctly"),
    ("native-workspace-safety", "Workspace recovery protects user data"),
    ("native-fresh-start", "First-run and recovery paths are safe"),
    ("native-physical-editing", "Physical inventory changes preserve counts"),
    ("native-proxy-editing", "Proxy edits preserve real ownership"),
    ("native-physical-quantity", "Card quantities remain consistent"),
    ("native-arena-editing", "Arena collection edits stay separate"),
    ("native-arena-import", "Arena inventory imports correctly"),
    ("native-scryfall-catalog-sync", "Catalog updates retain data safely"),
    ("native-deck-editing", "Deck edits and saves preserve card contents"),
    ("native-deck-owned-picker", "Deck card selection follows ownership rules"),
    ("native-deck-library-cache", "Saved deck library caches update correctly"),
    ("native-deck-cover-cache", "Deck artwork caches update correctly"),
    ("native-theme", "Themes retain expected behavior"),
    ("native-deck-analytics", "Deck performance metrics calculate correctly"),
    ("native-deck-preflight", "Deck validation catches invalid lists"),
    ("native-deck-interchange", "Deck export and import are compatible"),
    ("native-catalog-filter", "Catalog search and filters work"),
    ("native-ownership-catalog", "Catalog ownership is shown accurately"),
    ("native-arena-presentation", "Arena inventory is presented correctly"),
    ("native-printing-artwork", "Card printings and artwork match"),
    ("native-physical-valuation", "Collection values use owned printings"),
    ("native-shortlist", "Shortlisted cards are maintained correctly"),
    ("native-card-role", "Card role classification remains stable"),
    ("native-excel-import", "Excel importing preserves collection data"),
    ("native-csv-import", "CSV importing handles collection data"),
    ("native-format-legality", "Deck format rules are enforced"),
    ("native-update", "App update logic protects installations"),
    ("native-full-backup-export", "Full backups include required data"),
    ("native-peer-sync", "Sync and merge preserve collection integrity"),
    ("native-visual-shell", "Windows workspace structure remains consistent"),
    ("native-release-notes", "Release notes satisfy required fields"),
    ("legacy-storage-import", "Legacy storage import remains compatible"),
    ("collection-catalog-review", "Catalog and collection review are accurate"),
    ("native-set-progress", "Set completion totals are correct"),
    ("edh-lab", "EDH Lab core rules and analysis work"),
    ("edh-lab-draft", "EDH Lab draft workflow behaves correctly"),
    ("edh-lab-land-advisor", "Land recommendations follow mana needs"),
    ("edh-lab-mana-base-builder", "Suggested mana bases meet deck requirements"),
};
var requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
bool listOnly = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--list") { listOnly = true; continue; }
    string? value = null;
    if (args[i] == "--suite" && i + 1 < args.Length) value = args[++i];
    else if (args[i].StartsWith("--suite=", StringComparison.Ordinal))
        value = args[i]["--suite=".Length..];
    else throw new ArgumentException("Unknown argument: " + args[i] +
        ". Use --list, --suite suite-id, or --suite=suite-id.");
    foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        requested.Add(item);
}
if (listOnly)
{
    foreach (var suite in suiteDescriptions)
        Console.WriteLine($"SUITE {suite.Id}: {suite.Description}");
    return;
}
var validIds = suiteDescriptions.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
var missing = requested.Where(x => !validIds.Contains(x)).ToArray();
if (missing.Length > 0) throw new ArgumentException(
    "Unknown suite(s): " + string.Join(", ", missing) + ". Run --list to see available names.");
bool Selected(string id) => requested.Count == 0 || requested.Contains(id);
void RunNamed(string id, string description, Action run)
{
    if (!Selected(id)) return;
    var timer = Stopwatch.StartNew();
    Console.WriteLine($"RUN  {id}: {description}");
    try
    {
        run();
        Console.WriteLine($"PASS {id}: {description} ({timer.ElapsedMilliseconds} ms)");
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"FAIL {id}: {description}. {error.Message}");
        throw;
    }
}
async Task RunNamedAsync(string id, string description, Func<Task> run)
{
    if (!Selected(id)) return;
    var timer = Stopwatch.StartNew();
    Console.WriteLine($"RUN  {id}: {description}");
    try
    {
        await run();
        Console.WriteLine($"PASS {id}: {description} ({timer.ElapsedMilliseconds} ms)");
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"FAIL {id}: {description}. {error.Message}");
        throw;
    }
}

var work = Path.Combine(Path.GetTempPath(), "arcane-next-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
var sourcePath = Path.Combine(work, "fixture-catalog.sqlite");
var invalidPath = Path.Combine(work, "not-a-catalog.sqlite");

try
{
    RunNamed("match-assignment", "Match results attach to the correct saved deck", () => MatchAssignmentChecks.Run(work));
    RunNamed("match-performance", "Win rates and match analytics exclude ambiguous results", () => MatchPerformanceChecks.Run());
    RunNamed("match-deck-attribution", "Arena matches link to verified deck identities", () => MatchDeckAttributionChecks.Run());
    RunNamed("arena-log-assignment", "Arena logs record matches and gameplay evidence safely", () => ArenaLogAssignmentChecks.Run(work));
    RunNamed("arena-queue-rank", "Queue and rank data is interpreted correctly", () => ArenaQueueRankChecks.Run(work));
    RunNamed("arena-rank-season", "Ranked seasons and resets are read correctly", () => ArenaRankSeasonChecks.Run(work));
    RunNamed("catalog-snapshot", "Synthetic SQLite read-only search and snapshot integrity", () =>
    {
    // Create exactly the schema columns consumed by the readonly snapshot,
    // plus a separate ownership table whose contents must remain untouched.
    using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
           { DataSource = sourcePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
    {
        source.Open();
        using (var schema = source.CreateCommand())
        {
            schema.CommandText = """
                CREATE TABLE variants (
                    print_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL COLLATE NOCASE,
                    setcode TEXT NOT NULL,
                    collector TEXT NOT NULL
                );
                CREATE INDEX idx_variants_name ON variants(name COLLATE NOCASE);
                CREATE TABLE ownership (name TEXT PRIMARY KEY, qty INTEGER NOT NULL);
                INSERT INTO ownership(name,qty) VALUES('Secret Test Collection',17);
                """;
            schema.ExecuteNonQuery();
        }
        using var tx = source.BeginTransaction();
        using var add = source.CreateCommand();
        add.Transaction = tx;
        add.CommandText = "INSERT INTO variants(print_id,name,setcode,collector) VALUES ($id,$name,$set,$collector)";
        foreach (var p in new[] { "$id", "$name", "$set", "$collector" })
            add.Parameters.Add(new SqliteParameter(p, ""));
        for (int i = 0; i < 25_000; i++)
        {
            add.Parameters["$id"].Value = $"printing-{i:D5}";
            add.Parameters["$name"].Value = $"Sample Card {i:D5}";
            add.Parameters["$set"].Value = $"S{i % 60:D2}";
            add.Parameters["$collector"].Value = (i % 450 + 1).ToString();
            add.ExecuteNonQuery();
        }
        add.Parameters["$id"].Value = "literal-percent";
        add.Parameters["$name"].Value = "Special%Literal";
        add.Parameters["$set"].Value = "TST";
        add.Parameters["$collector"].Value = "1";
        add.ExecuteNonQuery();
        add.Parameters["$id"].Value = "literal-underscore";
        add.Parameters["$name"].Value = "Special_Literal";
        add.Parameters["$collector"].Value = "2";
        add.ExecuteNonQuery();
        // EXACTLY 200 matching rows must not be mislabeled as truncated.
        for (int i = 0; i < 200; i++)
        {
            add.Parameters["$id"].Value = $"exact-{i:D3}";
            add.Parameters["$name"].Value = $"Exactly Two Hundred {i:D3}";
            add.Parameters["$collector"].Value = (i + 1).ToString();
            add.ExecuteNonQuery();
        }
        tx.Commit();
    }

    var before = Hash(sourcePath);
    var tempPrefix = Path.GetTempPath();
    var priorSnapshots = Directory.GetFiles(tempPrefix, "arcane-vault-next-catalog-*.sqlite").ToHashSet(StringComparer.OrdinalIgnoreCase);

    using (var snapshot = CatalogSnapshot.Open(sourcePath))
    {
        Assert(snapshot.TotalPrintings == 25_202, "Snapshot row count changed");
        Assert(Hash(sourcePath) == before, "Source SQLite file was modified by snapshot creation");
        var prefix = snapshot.SearchPrefix("Sample Card 000");
        Assert(prefix.Rows.Count == 100, "Expected 100 distinct sample printings");
        Assert(!prefix.MayHaveMore, "Short prefix query cannot have extra rows");
        Assert(prefix.Rows.All(c => c.Name.StartsWith("Sample Card 000", StringComparison.OrdinalIgnoreCase)),
            "Prefix query returned unrelated printing");
        var limited = snapshot.SearchPrefix("Sample Card", 200);
        Assert(limited.Rows.Count == 200 && limited.MayHaveMore, "Large query not bounded at 200");
        var exactly = snapshot.SearchPrefix("Exactly Two Hundred", 200);
        Assert(exactly.Rows.Count == 200 && !exactly.MayHaveMore, "Exactly 200 results flagged as truncated");
        Assert(snapshot.SearchPrefix("Special%").Rows.Single().Name == "Special%Literal", "Literal percent wildcard escaped");
        Assert(snapshot.SearchPrefix("Special_").Rows.Single().Name == "Special_Literal", "Literal underscore wildcard escaped");
        Assert(snapshot.SearchPrefix("' OR 1=1 --").Rows.Count == 0, "User query must be SQL parameterized");

        // Modify *only* a synthetic source after the snapshot; the snapshot
        // must stay immutable until disposed. This is never run on real data.
        using (var modify = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = sourcePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            modify.Open();
            using var insert = modify.CreateCommand();
            insert.CommandText = "INSERT INTO variants(print_id,name,setcode,collector) VALUES ('new-print','New Post-Snapshot Card','TST','99')";
            insert.ExecuteNonQuery();
        }
        Assert(snapshot.SearchPrefix("New Post-Snapshot").Rows.Count == 0, "Snapshot changed after source updated");
        var samples = new List<double>();
        for (var i = 0; i < 30; i++)
        {
            var timer = Stopwatch.StartNew();
            Assert(snapshot.SearchPrefix("Sample Card 000").Rows.Count == 100, "Repeated benchmark query differed");
            samples.Add(timer.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        var p50 = samples[(int)Math.Ceiling(.50 * samples.Count) - 1];
        var p95 = samples[(int)Math.Ceiling(.95 * samples.Count) - 1];
        Assert(p50 >= 0 && p95 >= p50, "Invalid performance samples");
        Console.WriteLine($"BASELINE: SQLite snapshot prefix search synthetic 25k rows p50={p50:F2}ms p95={p95:F2}ms (CI hardware; not a user-device result)");
    }

    // Snapshot temp file must be removed on Dispose.
    var afterSnapshots = Directory.GetFiles(tempPrefix, "arcane-vault-next-catalog-*.sqlite");
    Assert(afterSnapshots.All(f => priorSnapshots.Contains(f)), "Leaked a temporary SQLite catalog snapshot");

    using (var refresh = CatalogSnapshot.Open(sourcePath))
        Assert(refresh.SearchPrefix("New Post-Snapshot").Rows.Count == 1, "New snapshot failed to see updated source");

    File.WriteAllText(invalidPath, "not a SQLite catalog");
    var rejected = false;
    try { using var bad = CatalogSnapshot.Open(invalidPath); }
    catch (SqliteException) { rejected = true; }
    catch (InvalidDataException) { rejected = true; }
    Assert(rejected, "Invalid source must be rejected");

        Console.WriteLine("PASS: 25,202-row read-only snapshot, original unchanged, bounded/escaped searches, snapshot isolation and cleanup");
    });

    RunNamed("ownership-golden", "Owned cards reconcile against the reference collection", () => OwnershipGoldenChecks.Run());
    RunNamed("migration-preview", "Legacy migration previews do not mutate live records", () => MigrationPreviewChecks.Run(work));
    RunNamed("offline-staging", "Imported data is safely staged", () => OfflineStagingChecks.Run(work));
    RunNamed("native-backup-reader", "Backups can be read and validated", () => NativeBackupReaderChecks.Run(work));
    RunNamed("native-large-collection", "Large collections remain supported", () => NativeLargeCollectionChecks.Run(work));
    RunNamed("native-workspace", "Local collection storage behaves correctly", () => NativeWorkspaceChecks.Run(work));
    RunNamed("native-workspace-safety", "Workspace recovery protects user data", () => NativeWorkspaceSafetyChecks.Run(work));
    RunNamed("native-fresh-start", "First-run and recovery paths are safe", () => NativeFreshStartChecks.Run(work));
    RunNamed("native-physical-editing", "Physical inventory changes preserve counts", () => NativePhysicalEditingChecks.Run(work));
    RunNamed("native-proxy-editing", "Proxy edits preserve real ownership", () => NativeProxyEditingChecks.Run(work));
    RunNamed("native-physical-quantity", "Card quantities remain consistent", () => NativePhysicalQuantityChecks.Run(work));
    RunNamed("native-arena-editing", "Arena collection edits stay separate", () => NativeArenaEditingChecks.Run(work));
    RunNamed("native-arena-import", "Arena inventory imports correctly", () => NativeArenaImportChecks.Run(work));
    await RunNamedAsync("native-scryfall-catalog-sync", "Catalog updates retain data safely", () => NativeScryfallCatalogSyncChecks.RunAsync(work));
    RunNamed("native-deck-editing", "Deck edits and saves preserve card contents", () => NativeDeckEditingChecks.Run(work));
    RunNamed("native-deck-owned-picker", "Deck card selection follows ownership rules", () => NativeDeckOwnedPickerChecks.Run());
    RunNamed("native-deck-library-cache", "Saved deck library caches update correctly", () => NativeDeckLibraryCacheChecks.Run(work));
    RunNamed("native-deck-cover-cache", "Deck artwork caches update correctly", () => NativeDeckCoverCacheChecks.Run(work));
    RunNamed("native-theme", "Themes retain expected behavior", () => NativeThemeChecks.Run(work));
    RunNamed("native-deck-analytics", "Deck performance metrics calculate correctly", () => NativeDeckAnalyticsChecks.Run(work));
    RunNamed("native-deck-preflight", "Deck validation catches invalid lists", () => NativeDeckPreflightChecks.Run(work));
    RunNamed("native-deck-interchange", "Deck export and import are compatible", () => NativeDeckInterchangeChecks.Run(work));
    RunNamed("native-catalog-filter", "Catalog search and filters work", () => NativeCatalogFilterChecks.Run(work));
    RunNamed("native-ownership-catalog", "Catalog ownership is shown accurately", () => NativeOwnershipCatalogChecks.Run(work));
    RunNamed("native-arena-presentation", "Arena inventory is presented correctly", () => NativeArenaPresentationChecks.Run(work));
    RunNamed("native-printing-artwork", "Card printings and artwork match", () => NativePrintingArtworkChecks.Run(work));
    RunNamed("native-physical-valuation", "Collection values use owned printings", () => NativePhysicalValuationChecks.Run(work));
    RunNamed("native-shortlist", "Shortlisted cards are maintained correctly", () => NativeShortlistChecks.Run(work));
    RunNamed("native-card-role", "Card role classification remains stable", () => NativeCardRoleChecks.Run(work));
    RunNamed("native-excel-import", "Excel importing preserves collection data", () => NativeExcelImportChecks.Run(work));
    RunNamed("native-csv-import", "CSV importing handles collection data", () => NativeCsvImportChecks.Run(work));
    RunNamed("native-format-legality", "Deck format rules are enforced", () => NativeFormatLegalityChecks.Run(work));
    RunNamed("native-update", "App update logic protects installations", () => NativeUpdateChecks.Run(work));
    RunNamed("native-full-backup-export", "Full backups include required data", () => NativeFullBackupExportChecks.Run(work));
    RunNamed("native-peer-sync", "Sync and merge preserve collection integrity", () => NativePeerSyncChecks.Run(work));
    RunNamed("native-visual-shell", "Windows workspace structure remains consistent", () => NativeVisualShellChecks.Run());
    RunNamed("native-release-notes", "Release notes satisfy required fields", () => NativeReleaseNotesChecks.Run());
    RunNamed("legacy-storage-import", "Legacy storage import remains compatible", () => LegacyStorageImportChecks.Run(work));
    RunNamed("collection-catalog-review", "Catalog and collection review are accurate", () => CollectionCatalogReviewChecks.Run(work));
    RunNamed("native-set-progress", "Set completion totals are correct", () => NativeSetProgressChecks.Run(work));
    RunNamed("edh-lab", "EDH Lab core rules and analysis work", () => EdhLabChecks.Run(work));
    RunNamed("edh-lab-draft", "EDH Lab draft workflow behaves correctly", () => EdhLabDraftChecks.Run(work));
    RunNamed("edh-lab-land-advisor", "Land recommendations follow mana needs", () => EdhLabLandAdvisorChecks.Run());
    RunNamed("edh-lab-mana-base-builder", "Suggested mana bases meet deck requirements", () => EdhLabManaBaseBuilderChecks.Run());

}
finally
{
    try { Directory.Delete(work, recursive: true); } catch (IOException) { }
}
