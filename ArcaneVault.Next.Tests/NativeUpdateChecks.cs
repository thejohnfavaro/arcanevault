using ArcaneVault.Next.Collection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class NativeUpdateChecks
{
    private static void Must(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Native upgrade safety: " + why);
    }

    private static void Reject(Action operation)
    {
        try { operation(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Unsafe native upgrade was not rejected.");
    }

    private static string Sha(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    private static object Release(string version, string host = "github.com",
        bool preview = true, bool checksum = true) => new
    {
        tag_name = "native-v" + version,
        draft = false,
        prerelease = preview,
        html_url = "https://github.com/thejohnfavaro/arcanevault/releases/tag/native-v" + version,
        assets = (checksum ?
            new[] { NativeUpdateService.ZipName, NativeUpdateService.ChecksumName } :
            new[] { NativeUpdateService.ZipName }).Select(name => new
        {
            name,
            browser_download_url = "https://" + host +
                "/thejohnfavaro/arcanevault/releases/download/native-v" +
                version + "/" + name
        }).ToArray()
    };

    private static string Zip(string folder, string filename, string version,
        string? malicious = null, string? badManifest = null,
        bool manifestUtf8Bom = false)
    {
        string path = Path.Combine(folder, filename);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            void Add(string entry, string value)
            {
                using var file = new StreamWriter(
                    zip.CreateEntry(entry).Open(), new UTF8Encoding(false));
                file.Write(value);
            }
            Add(NativeUpdateService.ExecutableName, "synthetic executable");
            string manifestJson = badManifest ?? JsonSerializer.Serialize(new
            {
                version, sourceSha = new string('a', 40), channel = "preview"
            });
            if (manifestUtf8Bom)
            {
                var manifestEntry = zip.CreateEntry(NativeUpdateService.ManifestName);
                using var manifestStream = manifestEntry.Open();
                manifestStream.Write(new byte[] { 0xEF, 0xBB, 0xBF });
                manifestStream.Write(Encoding.UTF8.GetBytes(manifestJson));
            }
            else
                Add(NativeUpdateService.ManifestName, manifestJson);
            Add("support/docs.txt", "This is a synthetic distribution.");
            if (malicious is not null) Add(malicious, "should not be extracted");
        }
        return path;
    }

    public static void Run(string work)
    {
        var installedVersion = new Version(0, 9, 0);
        var json = JsonSerializer.Serialize(new object[]
        {
            Release("0.9.0"), Release("0.9.1"), Release("0.9.3", "evil.example"),
            Release("1.0.0", preview: false),
            Release("0.9.4", checksum: false),
            Release("0.9.2")
        });
        var selected = NativeUpdateService.SelectLatest(json, installedVersion);
        Must(selected is not null && selected.Tag == "native-v0.9.2" &&
             selected.Version == new Version(0, 9, 2),
            "Only newer, official, complete native preview releases can be selected.");
        Reject(() => NativeUpdateService.SelectLatest("[{not valid JSON",
            installedVersion));
        var badReleaseShape = JsonSerializer.Serialize(new object[] {
            new { tag_name = "native-v0.9.99", prerelease = true, draft = false,
                  html_url = 123, assets = Array.Empty<object>() },
            Release("0.9.1")
        });
        Must(NativeUpdateService.SelectLatest(badReleaseShape, installedVersion)?.Tag ==
             "native-v0.9.1",
            "Non-string URLs in malformed releases must be ignored without crashing.");

        Must(NativeUpdateService.SelectLatest(json, new Version(0, 9, 2)) is null,
            "Must not reinstall older versions or incorrectly treat untrusted releases as updates.");
        var crowded = new List<object>();
        for (int i = 0; i < 98; i++)
            crowded.Add(new { tag_name = "windows-v5.1." + i, draft = false,
                prerelease = false });
        crowded.Add(Release("0.9.1"));
        var legacyHeavy = NativeUpdateService.SelectLatest(
            JsonSerializer.Serialize(crowded), installedVersion);
        Must(legacyHeavy?.Tag == "native-v0.9.1",
            "The native preview channel must remain separate from legacy Windows releases.");
        Must(NativeUpdateService.ParseChecksum(
            new string('a', 64) + "  " + NativeUpdateService.ZipName) == new string('a', 64),
            "Official sha256sum format must be parsed.");
        Reject(() => NativeUpdateService.ParseChecksum("bad-checksum"));

        string upgrade = Path.Combine(work, "upgrade-tests");
        Directory.CreateDirectory(upgrade);
        string privateData = Path.Combine(work, "synthetic-user-data");
        Directory.CreateDirectory(privateData);
        string nativeWorkspace = Path.Combine(privateData, "native-workspace.sqlite");
        string catalog = Path.Combine(privateData, "native-catalog.sqlite");
        string recovery = Path.Combine(privateData, "native-workspace.rollback-001.sqlite");
        File.WriteAllText(nativeWorkspace, "fake physical, Arena, decks, artwork");
        File.WriteAllText(catalog, "fake local Scryfall catalog");
        File.WriteAllText(recovery, "fake rollback history");
        var before = new[] { nativeWorkspace, catalog, recovery }.Select(Sha).ToArray();

        string zip = Zip(upgrade, "good.zip", "0.9.1");
        string root = Path.Combine(work, "separate-app-versions");
        var installed = NativeUpdateService.InstallVerifiedArchive(
            zip, Sha(zip), new Version(0, 9, 1), root);
        Must(File.Exists(installed.ExecutablePath) &&
             File.Exists(Path.Combine(installed.Directory, "support", "docs.txt")) &&
             installed.Directory.StartsWith(root, StringComparison.OrdinalIgnoreCase),
            "New executable must be installed in a new version directory.");
        // Windows PowerShell 5.1 writes a UTF-8 BOM by default.
        // A valid signed/checksummed native archive must accept that encoding
        // without weakening manifest identity, archive hash or data isolation.
        string bomZip = Zip(upgrade, "bom-manifest.zip", "0.9.6",
            manifestUtf8Bom: true);
        var bomInstalled = NativeUpdateService.InstallVerifiedArchive(
            bomZip, Sha(bomZip), new Version(0, 9, 6), root);
        Must(File.Exists(bomInstalled.ExecutablePath),
            "A checksummed native preview archive with a UTF-8 BOM must install.");
        string bomMismatch = Zip(upgrade, "bom-wrong-identity.zip", "0.9.7",
            manifestUtf8Bom: true);
        Reject(() => NativeUpdateService.InstallVerifiedArchive(
            bomMismatch, Sha(bomMismatch), new Version(0, 9, 8), root));

        string malformedJsonZip = Zip(upgrade, "broken-json-manifest.zip", "0.9.10",
            badManifest: "{not valid JSON");
        Reject(() => NativeUpdateService.InstallVerifiedArchive(
            malformedJsonZip, Sha(malformedJsonZip), new Version(0, 9, 10), root));
        string nonStringChannel = Zip(upgrade, "nonstring-channel-manifest.zip", "0.9.11",
            badManifest: JsonSerializer.Serialize(new {
                version = "0.9.11", sourceSha = new string('a', 40), channel = 123
            }));
        Reject(() => NativeUpdateService.InstallVerifiedArchive(
            nonStringChannel, Sha(nonStringChannel), new Version(0, 9, 11), root));
        Must(!Directory.Exists(Path.Combine(root, "v0.9.10")) &&
             !Directory.Exists(Path.Combine(root, "v0.9.11")) &&
             !Directory.GetDirectories(root, ".stage-*").Any(),
            "Malformed manifests must never publish an install or leave staging files.");

        var after = new[] { nativeWorkspace, catalog, recovery }.Select(Sha).ToArray();
        Must(before.SequenceEqual(after),
            "An app-only update must leave workspace, catalog and rollback byte-identical.");

        string oldExeHash = Sha(installed.ExecutablePath);
        var reused = NativeUpdateService.InstallVerifiedArchive(
            zip, Sha(zip), new Version(0, 9, 1), root);
        Must(reused.ExecutablePath == installed.ExecutablePath &&
             Sha(installed.ExecutablePath) == oldExeHash,
            "An identical, previously staged version should be safely reusable.");
        string tamperZip = Zip(upgrade, "tamper-fixture.zip", "0.9.5");
        var tamperInstall = NativeUpdateService.InstallVerifiedArchive(
            tamperZip, Sha(tamperZip), new Version(0, 9, 5), root);
        File.AppendAllText(tamperInstall.ExecutablePath, "tampered");
        string tamperedHash = Sha(tamperInstall.ExecutablePath);
        Reject(() => NativeUpdateService.InstallVerifiedArchive(
            tamperZip, Sha(tamperZip), new Version(0, 9, 5), root));
        Must(Sha(tamperInstall.ExecutablePath) == tamperedHash &&
             Sha(installed.ExecutablePath) == oldExeHash,
            "An altered staged version must never be overwritten or silently reused.");

        string invalidDest = Path.Combine(work, "invalid-installations");
        Reject(() => NativeUpdateService.InstallVerifiedArchive(
            zip, new string('0', 64), new Version(0, 9, 1), invalidDest));
        Must(!Directory.Exists(invalidDest),
            "Checksum rejection must occur before creating an install directory.");

        string mismatch = Zip(upgrade, "wrong-identity.zip", "0.9.2");
        Reject(() => NativeUpdateService.InstallVerifiedArchive(
            mismatch, Sha(mismatch), new Version(0, 9, 3), root));
        Must(!Directory.Exists(Path.Combine(root, "v0.9.3")),
            "A manifest/version mismatch must not publish an installation.");

        string badPath = Zip(upgrade, "traversal.zip", "0.9.4",
            malicious: "../escape-native-test.txt");
        Reject(() => NativeUpdateService.InstallVerifiedArchive(
            badPath, Sha(badPath), new Version(0, 9, 4), root));
        Must(!File.Exists(Path.Combine(root, "escape-native-test.txt")) &&
             !Directory.Exists(Path.Combine(root, "v0.9.4")),
            "ZIP path traversal must not create files outside staging.");

        Must(before.SequenceEqual(new[] { nativeWorkspace, catalog, recovery }.Select(Sha)) &&
             Sha(installed.ExecutablePath) == oldExeHash,
            "All failure paths must preserve old executable and user data.");
    }
}
