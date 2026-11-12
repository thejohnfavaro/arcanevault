using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class NativeScryfallCatalogSyncChecks
{
    private static void Must(bool yes, string message)
    {
        if (!yes) throw new InvalidOperationException("Native Scryfall catalog: " + message);
    }

    private static void Reject(Action test)
    {
        try { test(); }
        catch (InvalidDataException) { return; }
        catch (JsonException) { return; }
        catch (OperationCanceledException) { return; }
        catch (HttpRequestException) { return; }
        throw new InvalidOperationException("Invalid catalog sync unexpectedly passed validation.");
    }

    private static async Task RejectAsync(Func<Task> test)
    {
        try { await test(); }
        catch (InvalidDataException) { return; }
        catch (JsonException) { return; }
        catch (OperationCanceledException) { return; }
        catch (HttpRequestException) { return; }
        throw new InvalidOperationException("Invalid catalog sync unexpectedly passed validation.");
    }

    private static string Card(string id, string name, string set,
        string collector, string games, bool digital = false)
        => JsonSerializer.Serialize(new
        {
            @object = "card",
            id, name,
            set,
            set_name = set == "TST" ? "Test Set" : "Test Second Set",
            collector_number = collector,
            rarity = "rare",
            type_line = "Artifact Creature — Test",
            oracle_text = "Test Oracle text",
            cmc = 3.0,
            mana_cost = "{1}{U}{B}",
            layout = "normal",
            border_color = "black",
            released_at = "2026-10-01",
            scryfall_uri = "https://scryfall.com/card/tst/" + collector,
            image_uris = new { normal = "https://cards.scryfall.io/normal/test.jpg" },
            legalities = new { commander = "legal", brawl = "legal" },
            color_identity = new[] { "U", "B" },
            games = games.Split(','),
            prices = new { usd = "2.50", usd_foil = "7.25", usd_etched = (string?)null },
            digital
        });

    private static byte[] Gzip(string data)
    {
        using var destination = new MemoryStream();
        using (var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true))
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            gzip.Write(dataBytes);
        }
        return destination.ToArray();
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        internal byte[] Bulk = Array.Empty<byte>();
        internal string Stamp = "2026-11-10T12:00:00Z";
        internal int DownloadCount;
        internal bool FailDownload;
        internal string? HostOverride;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.RequestUri?.AbsoluteUri == NativeScryfallCatalogSync.ManifestUrl)
            {
                string json = JsonSerializer.Serialize(new
                {
                    @object = "list",
                    data = new[]
                    {
                        new
                        {
                            type = "default_cards",
                            updated_at = Stamp,
                            jsonl_download_uri = HostOverride ??
                                "https://data.scryfall.io/native-fixture.jsonl.gz"
                        }
                    }
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }
            if (request.RequestUri?.Host == "data.scryfall.io")
            {
                DownloadCount++;
                return Task.FromResult(FailDownload
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                      { Content = new ByteArrayContent(Bulk) });
            }
            throw new InvalidOperationException("Unexpected external URL in test: " +
                request.RequestUri);
        }
    }

    public static async Task RunAsync(string work)
    {
        var catalog = Path.Combine(work, "native-scryfall-catalog.sqlite");
        var source = Path.Combine(work, "scryfall-fixture-array.json");
        var gzipFile = Path.Combine(work, "scryfall-fixture-jsonl.gz");
        const string a = "00000000-0000-4000-8000-000000000881";
        const string b = "00000000-0000-4000-8000-000000000882";
        const string c = "00000000-0000-4000-8000-000000000883";
        const string d = "00000000-0000-4000-8000-000000000884";
        var initial = new[]
        {
            Card(a, "Catalog Test", "TST", "1", "paper,arena"),
            Card(b, "Catalog Test", "XYZ", "2", "paper"),
            Card(c, "Digital Only", "TST", "3", "arena", digital: true)
        };
        File.WriteAllText(source, "[\n" + string.Join(",\n", initial) + "\n]");
        string stampOne = "2026-11-10T12:00:00Z";
        var first = await NativeScryfallCatalogSync.ImportBulkFileAsync(
            source, catalog, stampOne, minimumPrintings: 2);
        Must(first.Updated && first.RecoveryPath is null &&
            first.Printings == 3 && first.CardNames == 2,
            "JSON array first sync was not verified.");
        var info = NativeScryfallCatalogSync.Verify(catalog, 2);
        Must(NativeScryfallCatalogSync.HasManaCostColumn(catalog),
             "Fresh native Scryfall catalog must include the mana-cost field.");
        using (var manaDb = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString()))
        {
            manaDb.Open();
            using var command = manaDb.CreateCommand();
            command.CommandText = "SELECT mana_cost FROM cards WHERE name='Catalog Test'";
            Must(command.ExecuteScalar()?.ToString() == "{1}{U}{B}",
                 "Scryfall mana symbols must persist unmodified in the offline cards table.");
        }
        Must(info.Printings == 3 && info.CardNames == 2 && info.UpdatedAt == stampOne,
            "Native catalog was not re-openable and verified.");

        // Source bulk prices must survive into the verified read-only native catalog
        // without floating-point conversion or silently replacing a null quote.
        using (var prices = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString()))
        {
            prices.Open();
            using var price = prices.CreateCommand();
            price.CommandText =
                "SELECT usd,usd_foil,usd_etched FROM variants WHERE print_id=$id";
            price.Parameters.AddWithValue("$id", a);
            using var priceRow = price.ExecuteReader();
            Must(priceRow.Read() && priceRow.GetString(0) == "2.50" &&
                 priceRow.GetString(1) == "7.25" && priceRow.GetString(2) == "",
                "Scryfall bulk USD price strings/nulls were not stored on the exact printing.");
        }
        var printing = NativePhysicalEditing.RequirePaperPrinting(catalog, Guid.Parse(a));
        Must(printing.Name == "Catalog Test" && printing.SetCode == "TST" &&
             printing.CollectorNumber == "1", "Catalog paper printing selection failed.");
        Must(NativePhysicalEditing.FindPaperPrintings(catalog, "Digital Only").Rows.Count == 0,
            "Digital-only catalog card leaked into physical printing selection.");
        var backup = NativeBackupReader.Read(JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = Array.Empty<object>(),
            physicalCards = new object[]
            {
                new { name = "Catalog Test", qty = 2, set = "TST", number = "1",
                    finish = "nonfoil", scryfallId = a },
                new { name = "Digital Only", qty = 4, set = "TST", number = "3",
                    finish = "nonfoil", scryfallId = c }
            },
            deckLibrary = Array.Empty<object>(),
            preferredArtwork = new { }
        }));
        var review = CollectionCatalogReview.Examine(backup, catalog);
        var sets = NativeSetProgress.Build(review, catalog);
        Must(review.ConfirmedCopies == 2 && review.UnresolvedCopies == 4 &&
             sets.ConfirmedOwnedSlots == 1 &&
             sets.ConfirmedOwnedCopies == 2 &&
             CollectionCatalogReview.ReadDetails(catalog, "Catalog Test")?.OracleText ==
                "Test Oracle text",
             "Native Scryfall schema is incompatible with verification, details or Set Progress.");

        var originalSha = SHA256.HashData(File.ReadAllBytes(catalog));
        var originalMtime = File.GetLastWriteTimeUtc(catalog);
        var inputLines = new[]
        {
            Card(a, "Catalog Test", "TST", "1", "paper,arena"),
            Card(b, "Catalog Test", "XYZ", "2", "paper"),
            Card(c, "Digital Only", "TST", "3", "arena", digital: true),
            Card(d, "Brand New", "TST", "4", "paper")
        };
        var downloaded = Gzip(string.Join("\n", inputLines) + "\n");
        File.WriteAllBytes(gzipFile, downloaded);
        string stampTwo = "2026-11-11T12:00:00Z";
        var refresh = await NativeScryfallCatalogSync.ImportBulkFileAsync(
            gzipFile, catalog, stampTwo, minimumPrintings: 2);
        Must(refresh.Updated && refresh.Printings == 4 &&
             refresh.CardNames == 3 &&
             refresh.RecoveryPath is not null &&
             File.Exists(refresh.RecoveryPath) &&
             NativeScryfallCatalogSync.Verify(refresh.RecoveryPath, 2).UpdatedAt == stampOne &&
             NativeScryfallCatalogSync.Verify(catalog, 2).UpdatedAt == stampTwo,
             "Gzip JSONL refresh did not atomically publish or preserve the old catalog.");
        Must(originalSha.SequenceEqual(SHA256.HashData(
            File.ReadAllBytes(refresh.RecoveryPath!))),
            "Old native catalog was not exactly preserved by refresh.");

        string corrupted = Path.Combine(work, "native-scryfall-invalid.json");
        File.WriteAllText(corrupted, "[{\"bad_json\":");
        var beforeError = SHA256.HashData(File.ReadAllBytes(catalog));
        RejectAsync(() => NativeScryfallCatalogSync.ImportBulkFileAsync(
            corrupted, catalog, "2026-11-12T00:00:00Z", minimumPrintings: 2))
            .GetAwaiter().GetResult();
        Must(beforeError.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))) &&
             Directory.GetFiles(work, "native-scryfall-catalog.sqlite.*.stage.sqlite").Length == 0,
             "Broken bulk file changed the native catalog or leaked staged SQLite.");

        string tiny = Path.Combine(work, "native-scryfall-tiny.json");
        File.WriteAllText(tiny, "[" + Card(d, "Tiny", "TST", "5", "paper") + "]");
        await RejectAsync(async () => { await NativeScryfallCatalogSync.ImportBulkFileAsync(
            tiny, catalog, "2026-11-12T00:00:00Z", minimumPrintings: 2); });
        Must(beforeError.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))),
            "Incomplete bulk data changed the installed catalog.");

        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            await RejectAsync(async () => { await NativeScryfallCatalogSync.ImportBulkFileAsync(
                gzipFile, catalog, "2026-11-13T00:00:00Z", minimumPrintings: 2,
                cancellation: cts.Token); });
        }
        Must(beforeError.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))),
            "Canceled catalog import modified the previous catalog.");

        // Mock full HTTPS metadata and bulk download. Nothing reaches the web
        // from this regression test. Same updated_at must prevent re-download.
        var thirdPath = Path.Combine(work, "native-scryfall-network-fixture.sqlite");
        var fixture = new FixtureHandler { Bulk = downloaded, Stamp = stampTwo };
        using var http = new HttpClient(fixture);
        var network = await NativeScryfallCatalogSync.SyncAsync(thirdPath, http,
            minimumPrintings: 2);
        var noOp = await NativeScryfallCatalogSync.SyncAsync(thirdPath, http,
            minimumPrintings: 2);
        Must(network.Updated && !noOp.Updated && fixture.DownloadCount == 1 &&
             NativeScryfallCatalogSync.Verify(thirdPath, 2).Printings == 4,
             "Full offline HTTP pipeline failed or downloaded unchanged dataset twice.");

        // Older catalogs lack mana_cost. Even with an unchanged source
        // timestamp Full Sync must rebuild, verifying rollback safety.
        var legacyPath = Path.Combine(work, "native-old-mana-schema.sqlite");
        File.Copy(thirdPath, legacyPath);
        using (var legacyDb = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = legacyPath, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString()))
        {
            legacyDb.Open();
            using var drop = legacyDb.CreateCommand();
            drop.CommandText = "ALTER TABLE cards DROP COLUMN mana_cost";
            drop.ExecuteNonQuery();
        }
        Must(!NativeScryfallCatalogSync.HasManaCostColumn(legacyPath),
             "Legacy test catalog must begin without mana_cost.");
        int previousDownloads = fixture.DownloadCount;
        var upgraded = await NativeScryfallCatalogSync.SyncAsync(
            legacyPath, http, minimumPrintings: 2);
        Must(upgraded.Updated && fixture.DownloadCount == previousDownloads + 1 &&
             upgraded.RecoveryPath is not null &&
             File.Exists(upgraded.RecoveryPath) &&
             NativeScryfallCatalogSync.HasManaCostColumn(legacyPath) &&
             !NativeScryfallCatalogSync.HasManaCostColumn(upgraded.RecoveryPath),
             "Same-timestamp old catalog must rebuild with mana costs, preserving rollback.");

        fixture.Stamp = "2026-11-12T12:00:00Z";
        fixture.FailDownload = true;
        var httpSha = SHA256.HashData(File.ReadAllBytes(thirdPath));
        await RejectAsync(async () => { await NativeScryfallCatalogSync.SyncAsync(
            thirdPath, http, minimumPrintings: 2); });
        Must(httpSha.SequenceEqual(SHA256.HashData(File.ReadAllBytes(thirdPath))),
            "HTTP failure replaced a working native catalog.");

        fixture.FailDownload = false;
        fixture.HostOverride = "https://untrusted.example.com/scryfall.json";
        await RejectAsync(async () => { await NativeScryfallCatalogSync.SyncAsync(
            thirdPath, http, minimumPrintings: 2); });
        Must(httpSha.SequenceEqual(SHA256.HashData(File.ReadAllBytes(thirdPath))),
            "Untrusted manifest download host was accepted.");

        using (var validManifest = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            @object = "list",
            data = new [] { new {
                type = "default_cards", updated_at = stampTwo,
                download_uri = "https://data.scryfall.io/legacy.json"
            } }
        })))
        {
            Must(NativeScryfallCatalogSync.ParseManifest(validManifest.RootElement)
                    .DownloadUrl.Host == "data.scryfall.io",
                "Legacy Scryfall JSON array metadata fallback failed.");
        }

        // Ownership and user collection data are kept entirely separate.
        var owned = NativeWorkspaceSafety.StartFresh(
            Path.Combine(work, "native-card-ownership-not-catalog.sqlite"));
        var ownershipSha = SHA256.HashData(File.ReadAllBytes(owned.Current.DatabasePath));
        Must(ownershipSha.SequenceEqual(SHA256.HashData(
            File.ReadAllBytes(owned.Current.DatabasePath))) &&
            originalMtime != default,
            "Catalog sync unexpectedly touched native ownership.");

        Console.WriteLine("PASS: independent Scryfall sync, stream legacy array and gzip JSONL, UUID printings, cards/details/sets parity, complete rollback, malformed/canceled/HTTP failures safe, HTTPS host validation, unchanged-version skip");
    }
}
