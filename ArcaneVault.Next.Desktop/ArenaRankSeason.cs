using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Official Arena season schedule, independently refreshed from Wizards while
/// the desktop app is running. No guesses about subsequent seasons. The last
/// verified future date survives offline restarts in a local atomic cache.
/// </summary>
internal static class ArenaRankSeason
{
    internal const string OfficialUrl =
        "https://magic.wizards.com/en/news/mtg-arena/ranked-season";
    internal sealed record Schedule(string season, string endsUtc, string checkedUtc,
        string sourceUrl, IReadOnlyDictionary<string,string> resetTable);
    internal static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArcaneVault","arena-ranked-season.json");

    // The November date is explicitly published, rather than an inferred end
    // of month. It must never be rolled forward to December without evidence.
    internal static Schedule? Initial(DateTimeOffset now) =>
        now < DateTimeOffset.Parse("2026-11-30T20:00:00Z",CultureInfo.InvariantCulture)
        ? new Schedule("November 2026","2026-11-30T20:00:00Z",
            "2026-11-18T00:00:00Z",OfficialUrl,new Dictionary<string,string>())
        : null;

    internal static Schedule? ParseOfficial(string html, DateTimeOffset checkedAt)
    {
        if(string.IsNullOrWhiteSpace(html) || html.Length>2_000_000)return null;
        string flat=WebUtility.HtmlDecode(Regex.Replace(html,
            "<[^>]*>"," ",RegexOptions.Singleline));
        flat=Regex.Replace(flat,@"\s+"," ");
        var season=Regex.Match(flat,@"(?<season>[A-Z][a-z]+\s+20\d{2})\s+ranked season begins.{0,240}?ends on\s+(?<date>[A-Z][a-z]+\s+\d{1,2},\s+20\d{2}),\s+at\s+.{0,65}?\((?<utc>\d{2}:\d{2})\s+UTC\)",
            RegexOptions.IgnoreCase);
        if(!season.Success)return null;
        if(!DateTime.TryParseExact(season.Groups["date"].Value,
            "MMMM d, yyyy",CultureInfo.GetCultureInfo("en-US"),
            DateTimeStyles.None,out var day))return null;
        if(!TimeOnly.TryParseExact(season.Groups["utc"].Value,"HH:mm",
            CultureInfo.InvariantCulture,DateTimeStyles.None,out var clock))return null;
        var end=new DateTimeOffset(day.Year,day.Month,day.Day,clock.Hour,clock.Minute,0,TimeSpan.Zero);
        if(end<=checkedAt || end>checkedAt.AddDays(65))return null;
        var maps=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        // Extract only entries explicitly present in the official reset table.
        // Never predict an unlisted tier or treat the current rank as a guaranteed finish.
        var section=flat.IndexOf("Seasonal Reset",StringComparison.OrdinalIgnoreCase);
        if(section>=0)
        {
            string excerpt=flat[section..Math.Min(flat.Length,section+5000)];
            var mapping=Regex.Matches(excerpt,
                @"(?<from>(?:Bronze|Silver|Gold|Platinum|Diamond)\s+Tier\s+[1-4]|Mythic)\s+(?<to>(?:Bronze|Silver|Gold|Platinum|Diamond)\s+Tier\s+[1-4])",
                RegexOptions.IgnoreCase);
            foreach(Match pair in mapping)
                maps[pair.Groups["from"].Value]=pair.Groups["to"].Value;
        }
        return new Schedule(season.Groups["season"].Value,end.ToString("O"),
            checkedAt.ToUniversalTime().ToString("O"),OfficialUrl,maps);
    }

    internal static Schedule? Read(string path,DateTimeOffset now)
    {
        try
        {
            if(!File.Exists(path) || new FileInfo(path).Length>100_000)return Initial(now);
            var saved=JsonSerializer.Deserialize<Schedule>(File.ReadAllText(path));
            if(saved is null || saved.sourceUrl!=OfficialUrl ||
                !DateTimeOffset.TryParse(saved.endsUtc,out var end) ||
                !DateTimeOffset.TryParse(saved.checkedUtc,out var checkedAt) ||
                checkedAt>now.AddMinutes(5) ||
                end<=now || end>now.AddDays(65))return Initial(now);
            return saved;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            JsonException or ArgumentException){return Initial(now);}
    }

    internal static async Task<Schedule?> CheckAsync(string path,
        DateTimeOffset now,HttpClient client,CancellationToken token)
    {
        using var response=await client.GetAsync(OfficialUrl,
            HttpCompletionOption.ResponseHeadersRead,token);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength is >2_000_000)
            throw new InvalidDataException("Official season page is unexpectedly large.");
        string body=await response.Content.ReadAsStringAsync(token);
        var current=ParseOfficial(body,now);
        if(current is null)return Read(path,now);
        string? directory=Path.GetDirectoryName(path);
        if(directory is not null)Directory.CreateDirectory(directory);
        string stage=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            await File.WriteAllTextAsync(stage,JsonSerializer.Serialize(current),token);
            File.Move(stage,path,overwrite:true);
        }
        finally { if(File.Exists(stage))File.Delete(stage); }
        return current;
    }

    internal static string? Predicted(Schedule? schedule,string? tier,int level,
        DateTimeOffset now)
    {
        if(schedule is null || !DateTimeOffset.TryParse(schedule.endsUtc,out var end) ||
            end<=now || string.IsNullOrWhiteSpace(tier))return null;
        string name=char.ToUpperInvariant(tier.Trim()[0])+tier.Trim()[1..].ToLowerInvariant();
        string key=name=="Mythic"?"Mythic":name+" Tier "+level;
        return schedule.resetTable.TryGetValue(key,out var result)?result:null;
    }
}
