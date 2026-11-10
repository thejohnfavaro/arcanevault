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

var work = Path.Combine(Path.GetTempPath(), "arcane-next-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
var sourcePath = Path.Combine(work, "fixture-catalog.sqlite");
var invalidPath = Path.Combine(work, "not-a-catalog.sqlite");

try
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

    OwnershipGoldenChecks.Run();
    MigrationPreviewChecks.Run(work);
    OfflineStagingChecks.Run(work);
    NativeBackupReaderChecks.Run(work);
    NativeWorkspaceChecks.Run(work);
    LegacyStorageImportChecks.Run(work);
    CollectionCatalogReviewChecks.Run(work);
    NativeSetProgressChecks.Run(work);
    EdhLabChecks.Run(work);

    Console.WriteLine("PASS: 25,202-row read-only snapshot, original unchanged, bounded/escaped searches, snapshot isolation and cleanup");
}
finally
{
    try { Directory.Delete(work, recursive: true); } catch (IOException) { }
}
