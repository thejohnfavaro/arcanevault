using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Text.Json;

internal static class NativeProxyEditingChecks
{
    private static void Must(bool yes, string why)
    {
        if (!yes) throw new InvalidOperationException("Native proxy inventory: " + why);
    }

    private static void Reject(Action work)
    {
        try { work(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Unsafe proxy edit was accepted.");
    }

    public static void Run(string work)
    {
        string path = Path.Combine(work, "proxy-independent-workspace.sqlite");
        var fresh = NativeWorkspaceSafety.StartFresh(path);
        Must(fresh.Created && NativeWorkspaceSafety.ListProxies(path).Count == 0,
            "Brand-new workspaces must have an empty, optional proxy ledger.");
        var baseline = fresh.Current.Collection;
        var first = NativeWorkspaceSafety.SetProxyQuantity(path, "Sol Ring", 2,
            fresh.Current.BackupSha256);
        var second = NativeWorkspaceSafety.SetProxyQuantity(path,
            "Third-party custom art", 3, first.Current.BackupSha256);
        var proxies = NativeWorkspaceSafety.ListProxies(path);
        Must(proxies.Count == 2 &&
             proxies.Single(x => x.Name == "Sol Ring").Quantity == 2 &&
             proxies.Single(x => x.Name == "Third-party custom art").Quantity == 3 &&
             proxies.All(x => x.Kind == "Proxy" &&
                x.Valuation.Contains("no price lookup")) &&
             second.Current.Collection.PhysicalCopies == 0 &&
             second.Current.Collection.ArenaCopies == 0 &&
             second.Current.Collection.PhysicalCards.SequenceEqual(baseline.PhysicalCards) &&
             second.Current.Collection.ArenaCards.SequenceEqual(baseline.ArenaCards),
             "Proxy copies must persist without manufacturing real inventory or prices.");
        var opened = NativeWorkspace.Open(path);
        Must(opened.BackupSha256 == second.Current.BackupSha256 &&
             NativeWorkspaceSafety.RecoveryPaths(path).Count == 2,
             "Every proxy change must verify SHA and preserve full prior workspace recovery.");

        var summary = NativeDeckOwnedPicker.Review(opened.Collection,
            NativeDeckOwnedPicker.Pool.Physical,
            "1 Sol Ring\n2 Third-party custom art", "", "60-card", proxies);
        Must(summary.MissingCopies == 0 && summary.ProxyCoveredCopies == 3 &&
             summary.CoveredCopies == 3 &&
             summary.Shortages.Count == 0 &&
             summary.Header.Contains("3 covered by Proxy"),
             "Proxy-backed paper deck copies must be covered but explicitly not owned.");
        var arena = NativeDeckOwnedPicker.Review(opened.Collection,
            NativeDeckOwnedPicker.Pool.Arena,
            "1 Sol Ring\n2 Third-party custom art", "", "Arena Brawl", proxies);
        Must(arena.MissingCopies == 3 && arena.ProxyCoveredCopies == 0,
             "Physical proxies must not be treated as Arena collection cards.");
        var preflight = NativeDeckPreflight.Check(opened.Collection,
            NativeDeckEditing.ParseMainboard("1 Sol Ring"),
            "", "Commander", catalogPath: null, proxies);
        Must(preflight.Findings.Any(x => x.Message.Contains("Proxy:", StringComparison.Ordinal) &&
             x.Message.Contains("not an owned printing", StringComparison.Ordinal)),
             "Deck save preflight must clearly call out paper proxies and tournament caveat.");

        int recoveryCount = NativeWorkspaceSafety.RecoveryPaths(path).Count;
        string unchangedSha = NativeWorkspace.Open(path).BackupSha256;
        Reject(() => NativeWorkspaceSafety.SetProxyQuantity(path,
            "Sol Ring", 3, first.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SetProxyQuantity(path,
            "Third-party custom art", 3, unchangedSha));
        Reject(() => NativeWorkspaceSafety.SetProxyQuantity(path, "Oops", -1, unchangedSha));
        Reject(() => NativeWorkspaceSafety.SetProxyQuantity(path,
            new string('X', 256), 1, unchangedSha));
        Reject(() => NativeWorkspaceSafety.SetProxyQuantity(path, "Not there", 0, unchangedSha));
        Must(NativeWorkspace.Open(path).BackupSha256 == unchangedSha &&
             NativeWorkspaceSafety.RecoveryPaths(path).Count == recoveryCount,
             "Rejected invalid/stale edits must leave the collection and recovery files untouched.");

        var reduced = NativeWorkspaceSafety.SetProxyQuantity(path, "Sol Ring", 1,
            unchangedSha);
        Must(NativeWorkspaceSafety.ListProxies(path).Single(x => x.Name == "Sol Ring")
            .Quantity == 1 &&
            reduced.Current.Collection.PhysicalCopies == 0,
            "Editing a proxy count must preserve independent real paper ownership.");
        var removed = NativeWorkspaceSafety.SetProxyQuantity(path,
            "Third-party custom art", 0, reduced.Current.BackupSha256);
        Must(NativeWorkspaceSafety.ListProxies(path).Count == 1 &&
             NativeWorkspaceSafety.ListProxies(path)[0].Name == "Sol Ring" &&
             removed.Current.Collection.PhysicalCopies == 0,
             "Removing a proxy must not delete physical inventory or other proxies.");

        // Malformed extras must be rejected rather than silently promoted to
        // physical cards or discarded during backup import/reopen.
        byte[] malformed = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2, cards = Array.Empty<object>(),
            physicalCards = Array.Empty<object>(), deckLibrary = Array.Empty<object>(),
            preferredArtwork = new { },
            proxyCards = new object[] { new { name = "Bad", qty = 10, kind = "Owned" } }
        });
        Reject(() => NativeWorkspace.ImportNewBytes(malformed,
            "malformed-proxy-ledger", Path.Combine(work, "bad-proxy-workspace.sqlite")));
        Must(!File.Exists(Path.Combine(work, "bad-proxy-workspace.sqlite")),
            "Malformed proxy data must not produce an installed native workspace.");

        // An explicit verified fresh start is the only intentional reset of
        // physical, Arena, deck and proxy data together.
        var cleared = NativeWorkspaceSafety.StartFresh(path,
            removed.Current.BackupSha256);
        Must(cleared.Current.Collection.PhysicalCopies == 0 &&
             NativeWorkspaceSafety.ListProxies(path).Count == 0 &&
             cleared.RecoveryPath is not null &&
             File.Exists(cleared.RecoveryPath),
             "Fresh start must clear proxies only with an explicit whole-workspace reset and recovery.");
    }
}
