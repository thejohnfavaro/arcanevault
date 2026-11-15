using System.Globalization;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only physical collection valuation history. Each observed day is
/// represented once; subsequent updates replace only that day's observation.
/// It never changes the ownership ledger or invents previous prices.
/// </summary>
public static class NativeValueHistory
{
    public sealed record Point(DateOnly Day, decimal Usd);
    private const int MaxDays = 730;
    public static string DefaultDirectory =>
        Path.Combine(NativeWorkspace.RootDirectory, "cache", "value-history");

    private static string PathFor(string? setCode, string? directory)
    {
        string key = string.IsNullOrEmpty(setCode) ? "collection" :
            setCode.Trim().ToLowerInvariant();
        if (key != "collection" &&
            (key.Length is < 2 or > 12 || !key.All(char.IsLetterOrDigit)))
            throw new ArgumentException("Invalid set code.", nameof(setCode));
        return Path.Combine(directory ?? DefaultDirectory, key + ".json");
    }

    public static IReadOnlyList<Point> Read(string? setCode = null,
        string? directory = null)
    {
        string path = PathFor(setCode, directory);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 150_000)
                return Array.Empty<Point>();
            var saved = JsonSerializer.Deserialize<List<Point>>(File.ReadAllText(path));
            return (saved ?? new List<Point>())
                .Where(p => p.Usd > 0m && p.Usd < 10_000_000_000m &&
                    p.Day <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                    p.Day >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-MaxDays)))
                .GroupBy(p => p.Day)
                .Select(g => g.Last())
                .OrderBy(p => p.Day)
                .TakeLast(MaxDays)
                .ToArray();
        }
        catch (Exception) { return Array.Empty<Point>(); }
    }

    public static void Record(string? setCode, decimal usd,
        string? directory = null, DateOnly? day = null)
    {
        if (usd <= 0m || usd >= 10_000_000_000m) return;
        DateOnly observed = day ?? DateOnly.FromDateTime(DateTime.UtcNow);
        string path = PathFor(setCode, directory);
        var series = Read(setCode, directory).Where(p => p.Day < observed)
            .Append(new Point(observed, usd))
            .OrderBy(p => p.Day).TakeLast(MaxDays).ToArray();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string payload = JsonSerializer.Serialize(series);
            if (File.Exists(path) && File.ReadAllText(path) == payload) return;
            string temp = path + ".tmp";
            File.WriteAllText(temp, payload);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception) { /* History is optional; ownership stays untouched. */ }
    }

    public static void RecordSnapshot(NativePhysicalValuation.Result total,
        IReadOnlyDictionary<string, NativeSetValuation.SetValue> sets,
        string? directory = null)
    {
        ArgumentNullException.ThrowIfNull(total);
        ArgumentNullException.ThrowIfNull(sets);
        if (total.PricedRows > 0)
            Record(null, total.PricedSubtotalUsd, directory);
        foreach (var (code, value) in sets)
            if (value.HasPrice) Record(code, value.PricedUsd, directory);
    }
}
