using System.Globalization;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only set-specific totals derived from the independently verified physical
/// printing/finish valuation. Persists ONLY first valid per-set price baselines,
/// never card ownership or live quotes. Separate from the Set Progress cache.
/// </summary>
public static class NativeSetValuation
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public sealed record SetValue(string SetCode, decimal PricedUsd, int PricedRows,
        int UnpricedRows, decimal? BaselineUsd)
    {
        public bool HasPrice => PricedRows > 0;
        public decimal? ChangeUsd => BaselineUsd.HasValue ? PricedUsd - BaselineUsd : null;
        public decimal? ChangePercent => BaselineUsd > 0m
            ? 100m * (PricedUsd - BaselineUsd.Value) / BaselineUsd.Value : null;
        public string ValueLabel => HasPrice
            ? $"OWNED {PricedUsd.ToString("C2", Us)}" : "";
        public string DeltaLabel => !ChangeUsd.HasValue ? "" :
            $"{(ChangeUsd >= 0m ? "+" : "−")}{Math.Abs(ChangeUsd.Value).ToString("C2", Us)} " +
            $"({(ChangePercent >= 0m ? "+" : "−")}{Math.Abs(ChangePercent ?? 0m):0.0}%)";
        public string DeltaColor => ChangeUsd > 0m ? "#63DD9B"
            : ChangeUsd < 0m ? "#F77C7C" : "#C5CEDE";
        public string CoverageLabel => HasPrice
            ? $"{PricedRows:N0} priced, {UnpricedRows:N0} unpriced printing rows" : "";
        public string DetailLabel => HasPrice
            ? $"Owned set value: {PricedUsd.ToString("C2", Us)}" +
              (ChangeUsd.HasValue ? $" • {DeltaLabel} lifetime" : "") +
              $" • {CoverageLabel} • Scryfall cached reference"
            : "Owned set value unavailable: no verified printing prices";
    }

    public static string DefaultPath => Path.Combine(
        NativeWorkspace.RootDirectory, "cache", "set-values", "baselines.json");

    public static IReadOnlyDictionary<string, SetValue> Calculate(
        NativePhysicalValuation.Result valuation, string? baselinePath = null)
    {
        ArgumentNullException.ThrowIfNull(valuation);
        string path = baselinePath ?? DefaultPath;
        var baseline = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 1_000_000)
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
                    File.ReadAllText(path));
                if (existing is not null)
                    foreach (var (set, amount) in existing)
                        if (set.Length is >= 2 and <= 12 &&
                            set.All(char.IsLetterOrDigit) &&
                            amount > 0m && amount < 10_000_000_000m)
                            baseline[set] = amount;
            }
        }
        catch (Exception) { /* A damaged baseline must not block read-only values. */ }

        var result = new Dictionary<string, SetValue>(StringComparer.OrdinalIgnoreCase);
        bool changed = false;
        foreach (var group in valuation.Lines
            .Where(line => line.Source.Confirmed &&
                !string.IsNullOrWhiteSpace(line.Source.ConfirmedSet))
            .GroupBy(line => line.Source.ConfirmedSet.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            decimal amount = group.Where(line => line.LineUsd.HasValue)
                .Sum(line => line.LineUsd!.Value);
            int priced = group.Count(line => line.LineUsd.HasValue);
            int missing = group.Count() - priced;
            if (priced > 0 && amount > 0m && !baseline.ContainsKey(group.Key))
            {
                baseline[group.Key] = amount;
                changed = true;
            }
            baseline.TryGetValue(group.Key, out var original);
            result[group.Key] = new SetValue(group.Key, amount,
                priced, missing, original > 0m ? original : null);
        }
        if (changed)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(baseline));
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception) { /* Never change inventory if baseline write fails. */ }
        }
        return result;
    }
}
