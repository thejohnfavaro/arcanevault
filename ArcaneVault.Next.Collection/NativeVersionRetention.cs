using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Collection;

/// <summary>Best-effort post-startup cleanup of *managed application binaries*.
/// Never touches the native collection, catalogs, user backups or sync snapshots.
/// Only runs when the current executable lives in the official versions root.
/// </summary>
public static class NativeVersionRetention
{
    public sealed record CleanupResult(int Deleted, int Preserved, int Failed);

    private static bool PlainDirectory(string path)
    {
        try
        {
            var directory=new DirectoryInfo(path);
            return directory.Exists &&
                (directory.Attributes & FileAttributes.ReparsePoint)==0;
        }
        catch { return false; }
    }

    private static bool ManagedVersion(string path,Version expected)
    {
        if(!PlainDirectory(path))return false;
        string exe=Path.Combine(path,NativeUpdateService.ExecutableName);
        string manifest=Path.Combine(path,NativeUpdateService.ManifestName);
        if(!File.Exists(exe)||!File.Exists(manifest))return false;
        try
        {
            var info=new FileInfo(manifest);
            if(info.Length is < 20 or > 65536 ||
                (info.Attributes & FileAttributes.ReparsePoint)!=0)return false;
            using var json=JsonDocument.Parse(File.ReadAllText(manifest));
            var root=json.RootElement;
            return root.ValueKind==JsonValueKind.Object &&
                root.TryGetProperty("version",out var version) &&
                Version.TryParse(version.GetString(),out var found) &&
                found==expected;
        }
        catch { return false; }
    }

    public static CleanupResult CleanupAfterVerifiedLaunch(
        string versionsRoot,string currentExecutable)
    {
        if(string.IsNullOrWhiteSpace(versionsRoot)||
           string.IsNullOrWhiteSpace(currentExecutable))
            return new CleanupResult(0,0,0);
        string root=Path.GetFullPath(versionsRoot)
            .TrimEnd(Path.DirectorySeparatorChar);
        string executable=Path.GetFullPath(currentExecutable);
        string active=Path.GetDirectoryName(executable)??"";
        if(!PlainDirectory(root)||!PlainDirectory(active)||
            !string.Equals(Path.GetDirectoryName(active),root,
                StringComparison.OrdinalIgnoreCase)||
            !string.Equals(Path.GetFileName(executable),
                NativeUpdateService.ExecutableName,
                StringComparison.OrdinalIgnoreCase))
            return new CleanupResult(0,0,0);
        string folder=Path.GetFileName(active);
        if(!Regex.IsMatch(folder,@"^v[0-9]+\.[0-9]+\.[0-9]+$",
            RegexOptions.CultureInvariant) ||
            !Version.TryParse(folder[1..],out var current) ||
            !ManagedVersion(active,current))
            return new CleanupResult(0,0,0);

        var previous=new List<(Version Version,string Path)>();
        foreach(var candidate in Directory.EnumerateDirectories(root,"v*",SearchOption.TopDirectoryOnly))
        {
            string name=Path.GetFileName(candidate);
            if(!Regex.IsMatch(name,@"^v[0-9]+\.[0-9]+\.[0-9]+$",
                RegexOptions.CultureInvariant) ||
                !Version.TryParse(name[1..],out var version) ||
                version>=current ||
                !ManagedVersion(candidate,version))
                continue;
            previous.Add((version,candidate));
        }
        previous.Sort((a,b)=>b.Version.CompareTo(a.Version));
        // The newest prior managed version is the rollback target.
        int deleted=0,failed=0;
        foreach(var candidate in previous.Skip(1))
        {
            try
            {
                // Revalidate immediately before deleting; skip any junction,
                // symlink, or unexpected file-system identity.
                if(!ManagedVersion(candidate.Path,candidate.Version))continue;
                if(Directory.EnumerateFileSystemEntries(candidate.Path,"*",
                    SearchOption.AllDirectories)
                    .Any(item=>(File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0))
                    continue;
                Directory.Delete(candidate.Path,recursive:true);
                deleted++;
            }
            catch { failed++; /* A locked binary is left for a later launch. */ }
        }
        return new CleanupResult(deleted,previous.Count>0?1:0,failed);
    }
}
