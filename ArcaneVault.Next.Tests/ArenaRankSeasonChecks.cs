using ArcaneVault.Next.Desktop;
using System.Text.Json;

internal static class ArenaRankSeasonChecks
{
    static void Check(bool condition,string message)
    {
        if(!condition)throw new InvalidOperationException("Season: "+message);
    }
    public static void Run(string work)
    {
        var now=DateTimeOffset.Parse("2026-11-18T18:00:00Z");
        const string html= """
            <html><h2>November 2026 Ranked Season</h2><p>The November 2026
            ranked season begins October 31, 2026, at 12:05 p.m. PT
            (19:05 UTC) and ends on November 30, 2026, at 12:00 p.m.
            PT (20:00 UTC).</p><h2>Seasonal Reset</h2>
            <table><tr><td>Previous Rank</td><td>New Rank</td></tr>
            <tr><td>Bronze Tier 4</td><td>Bronze Tier 4</td></tr>
            <tr><td>Gold Tier 2</td><td>Silver Tier 3</td></tr>
            <tr><td>Gold Tier 1</td><td>Silver Tier 2</td></tr></table>
            </html>
            """;
        var parsed=ArenaRankSeason.ParseOfficial(html,now);
        Check(parsed is not null,"Read published season date");
        Check(parsed!.endsUtc.StartsWith("2026-11-30T20:00:00"),
            "Keep the published UTC hour, not the computer timezone");
        Check(parsed.season=="November 2026","Preserve official season month");
        Check(ArenaRankSeason.Predicted(parsed,"Gold",2,now)=="Silver Tier 3",
            "Verified tier prediction");
        Check(ArenaRankSeason.Predicted(parsed,"Diamond",4,now) is null,
            "Do not extrapolate missing rows of official table");
        Check(ArenaRankSeason.ParseOfficial(html,now.AddDays(20)) is null,
            "Refuse a schedule that already ended");
        Check(ArenaRankSeason.ParseOfficial("invalid",now) is null,
            "Refuse a format change rather than inventing a reset date");
        var cache=Path.Combine(work,"arena-rank-season-fixture.json");
        File.WriteAllText(cache,JsonSerializer.Serialize(parsed));
        Check(ArenaRankSeason.Read(cache,now)?.season=="November 2026",
            "Persist verified season for offline viewing");
        Check(ArenaRankSeason.Read(cache,now.AddDays(20)) is null,
            "Expired season must not be displayed as the next rank reset");
        Check(ArenaRankSeason.Initial(now)?.season=="November 2026",
            "Only published November seed may be shown offline");
        Check(ArenaRankSeason.Initial(now.AddDays(20)) is null,
            "Never roll a hardcoded date into an unverified future month");
    }
}
