using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Peer-sync contract v1. Read the exact native schema-2 backup, produce a
/// portable envelope and compare records using stable identities. No network,
/// cloud, automatic merge, or writes. The preview is a safety gate for the
/// later authenticated, explicitly approved apply path.
/// </summary>
public static class NativePeerSync
{
    public const string Format = "arcane-vault-peer-sync";
    public const int Schema = 1;
    public const int MaxBytes = 30 * 1024 * 1024;

    public sealed record Change(string Group, string Key, string Action,
        string? LocalJson, string? RemoteJson);
    public sealed record Preview(string Device, string Revision, string CurrentSha,
        IReadOnlyList<Change> Changes, int EqualRecords, int Conflicts)
    {
        public int Additions => Changes.Count(c => c.Action == "Add");
        public int Updates => Changes.Count(c => c.Action == "Different");
        public int Removals => Changes.Count(c => c.Action == "Absent in peer");
        public bool HasChanges => Changes.Count > 0;
        public bool SafeToAutoApply => false; // no common-base proof in v1
    }

    public static string DeviceId()
    {
        string location=Path.Combine(NativeWorkspace.RootDirectory,"peer-device-id.txt");
        Directory.CreateDirectory(NativeWorkspace.RootDirectory);
        if(File.Exists(location))
        {
            string existing=File.ReadAllText(location).Trim();
            if(Guid.TryParse(existing,out Guid valid) && valid!=Guid.Empty)
                return valid.ToString("D");
            throw new InvalidDataException("Saved peer identity is invalid; do not silently replace it.");
        }
        string created=Guid.NewGuid().ToString("D");
        try { File.WriteAllText(location,created); }
        catch(IOException) when(File.Exists(location))
        {
            string saved=File.ReadAllText(location).Trim();
            if(Guid.TryParse(saved,out Guid parsed)&&parsed!=Guid.Empty)
                return parsed.ToString("D");
            throw;
        }
        return created;
    }

    private static string Digest(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] ReadCurrent(string path)
    {
        var current = NativeWorkspace.Open(path);
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = current.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
        var bytes = (byte[])(cmd.ExecuteScalar() ??
            throw new InvalidDataException("Current workspace has no preserved backup."));
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
            throw new InvalidDataException("Peer sync workspace exceeds 30 MiB.");
        return bytes;
    }

    private static JsonObject RequireData(JsonNode? value)
    {
        var document = value as JsonObject ??
            throw new InvalidDataException("Peer sync data must be an object.");
        if (document["schema"]?.GetValue<int>() != 2)
            throw new InvalidDataException("Expected Arcane Vault schema-2 workspace.");
        foreach (var key in new[] { "cards", "physicalCards", "deckLibrary" })
            if (document[key] is not JsonArray)
                throw new InvalidDataException("Missing sync records: " + key);
        if (document["preferredArtwork"] is not JsonObject)
            throw new InvalidDataException("Missing sync artwork preferences.");
        return document;
    }

    public static byte[] Export(string nativePath, string deviceId)
    {
        if (!Guid.TryParse(deviceId, out var guid) || guid == Guid.Empty)
            throw new InvalidDataException("Peer sync requires a stable device UUID.");
        byte[] backup = ReadCurrent(nativePath);
        var data = RequireData(JsonNode.Parse(backup));
        var result = new JsonObject
        {
            ["format"] = Format,
            ["schema"] = Schema,
            ["deviceId"] = guid.ToString("D"),
            ["revision"] = Digest(backup),
            ["createdUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["data"] = data.DeepClone()
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(result);
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException("Peer sync export exceeds 30 MiB.");
        return bytes;
    }

    private static (string Device, string Revision, JsonObject Data) ParsePeer(byte[] bytes)
    {
        if (bytes is not { Length: > 20 and <= MaxBytes })
            throw new InvalidDataException("Peer sync file is empty or too large.");
        var root = JsonNode.Parse(bytes) as JsonObject ??
            throw new InvalidDataException("Invalid peer sync JSON.");
        if (root["format"]?.GetValue<string>() != Format ||
            root["schema"]?.GetValue<int>() != Schema)
            throw new InvalidDataException("Unsupported peer sync format/version.");
        string device = root["deviceId"]?.GetValue<string>() ?? "";
        if (!Guid.TryParse(device, out var id) || id == Guid.Empty)
            throw new InvalidDataException("Peer device identity is invalid.");
        var data = RequireData(root["data"]);
        string revision = root["revision"]?.GetValue<string>() ?? "";
        if (revision.Length != 64 || !revision.All(Uri.IsHexDigit))
            throw new InvalidDataException("Missing peer snapshot fingerprint.");
        // Fingerprints are metadata until a shared canonical serialization is
        // adopted. Never treat them alone as authority to replace records.
        return (id.ToString("D"), revision, data);
    }

    private static string Key(string group, JsonNode? row)
    {
        var obj = row as JsonObject ??
            throw new InvalidDataException("Sync inventory contains a non-object record.");
        string Value(string name) => (obj[name]?.ToString() ?? "").Trim().ToLowerInvariant();
        string result = group switch
        {
            "cards" => Value("name"),
            "physicalCards" => string.Join("|", new[] {
                Value("scryfallId"),Value("name"),Value("set"),Value("number"),
                Value("finish"),Value("language"),Value("condition"),
                Value("location"),Value("purchasePrice")
            }.Select(v => v.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+v)),
            "deckLibrary" => Value("id"),
            _ => throw new InvalidDataException("Unexpected record collection.")
        };
        if (string.IsNullOrWhiteSpace(result.Replace("|", "")))
            throw new InvalidDataException("Sync records require stable identities.");
        if (group == "deckLibrary" && string.IsNullOrEmpty(result))
            throw new InvalidDataException("Saved decks need stable IDs before syncing.");
        return result;
    }

    // Canonical comparison ignores serializer-specific JSON object key order.
    // Arrays keep their order: deck mainboards and history are not sortable.
    private static string Canonical(JsonNode? node)
    {
        if (node is null) return "null";
        if (node is JsonObject obj)
            return "{" + string.Join(",",obj.OrderBy(p=>p.Key,StringComparer.Ordinal)
                .Select(p=>JsonSerializer.Serialize(p.Key)+":"+Canonical(p.Value))) + "}";
        if (node is JsonArray list)
            return "["+string.Join(",",list.Select(Canonical))+"]";
        return node.ToJsonString();
    }

    private static Dictionary<string, string> Index(string group, JsonArray rows)
    {
        var indexed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in rows)
        {
            string key = Key(group, record);
            // Duplicate identities cannot be reconciled safely by index.
            // Prevent overwriting any such rows until they are normalized.
            if (!indexed.TryAdd(key, Canonical(record)))
                throw new InvalidDataException("Duplicate " + group +
                    " record identity: " + key);
        }
        return indexed;
    }

    public sealed record ThreeWayRecord(string Group,string Key,string Decision,
        bool IncomingDeletion);
    public sealed record ThreeWayPlan(string LocalRevision,string IncomingRevision,
        string BaselineRevision,IReadOnlyList<ThreeWayRecord> Records)
    {
        public int IncomingOnly => Records.Count(r=>r.Decision=="Incoming only");
        public int LocalOnly => Records.Count(r=>r.Decision=="Local only");
        public int Conflicts => Records.Count(r=>r.Decision=="Conflict");
        // Comparing a user-supplied baseline does not prove it was approved by both
        // devices. No automatic application until durable pairing and recovery.
        public bool SafeToAutoApply => false;
    }

    private static Dictionary<string,string> Flatten(JsonObject snapshot)
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var group in new[]{"cards","physicalCards","deckLibrary"})
        {
            var entries=Index(group,(JsonArray)snapshot[group]!);
            foreach(var pair in entries)
                result.Add(group+"\u001f"+pair.Key,pair.Value);
        }
        var artwork=(JsonObject)snapshot["preferredArtwork"]!;
        foreach(var pair in artwork)
            result.Add("preferredArtwork\u001f"+pair.Key,Canonical(pair.Value));
        return result;
    }

    /// <summary>Three-way classification only. It neither trusts the supplied
    /// baseline as paired nor applies changes to local or remote data.</summary>
    public static ThreeWayPlan CompareWithBaseline(
        string nativePath,byte[] incoming,byte[] lastCommonSnapshot)
    {
        var remote=ParsePeer(incoming);
        var baseSnapshot=ParsePeer(lastCommonSnapshot);
        byte[] bytes=ReadCurrent(nativePath);
        var local=RequireData(JsonNode.Parse(bytes));
        var a=Flatten(local);
        var b=Flatten(baseSnapshot.Data);
        var c=Flatten(remote.Data);
        var output=new List<ThreeWayRecord>();
        foreach(var combined in a.Keys.Union(b.Keys,StringComparer.Ordinal)
            .Union(c.Keys,StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            a.TryGetValue(combined,out var own);
            b.TryGetValue(combined,out var ancestor);
            c.TryGetValue(combined,out var incomingValue);
            if(own==incomingValue)continue;
            string decision=own==ancestor?"Incoming only":
                incomingValue==ancestor?"Local only":"Conflict";
            int divider=combined.IndexOf('\u001f');
            output.Add(new ThreeWayRecord(combined[..divider],combined[(divider+1)..],
                decision,incomingValue is null));
        }
        return new ThreeWayPlan(Digest(bytes),remote.Revision,baseSnapshot.Revision,output);
    }

    public sealed record MergeProposal(
        string ExpectedCurrentSha,
        string RemoteDevice,
        int IncomingChanges,
        int LocalChanges,
        byte[] CandidateBackup,
        bool HasChanges);

    /// <summary>
    /// Prepare a three-way, non-destructive merge. A missing incoming record
    /// is NOT a tombstone and never deletes an existing local record. Reject
    /// simultaneous edits, duplicate record IDs, invalid baselines, and opaque
    /// changes that cannot be reconciled reliably.
    /// </summary>
    public static MergeProposal PrepareMerge(
        string nativePath,byte[] incoming,byte[] commonBaseline)
    {
        var remote=ParsePeer(incoming);
        var ancestor=ParsePeer(commonBaseline);
        if(remote.Device == DeviceId())
            throw new InvalidDataException("Cannot merge a snapshot from this same device.");
        byte[] localBytes=ReadCurrent(nativePath);
        var local=RequireData(JsonNode.Parse(localBytes));
        var baseData=ancestor.Data;
        var remoteData=remote.Data;

        var candidate=(JsonObject)local.DeepClone();
        int incomingCount=0,localCount=0;
        foreach(string group in new[]{"cards","physicalCards","deckLibrary"})
        {
            var a=Index(group,(JsonArray)local[group]!);
            var b=Index(group,(JsonArray)baseData[group]!);
            var c=Index(group,(JsonArray)remoteData[group]!);
            var localNodes=((JsonArray)local[group]!).ToDictionary(
                row=>Key(group,row),row=>row!,StringComparer.OrdinalIgnoreCase);
            var remoteNodes=((JsonArray)remoteData[group]!).ToDictionary(
                row=>Key(group,row),row=>row!,StringComparer.OrdinalIgnoreCase);
            var merged=new Dictionary<string,JsonNode>(StringComparer.OrdinalIgnoreCase);
            foreach(string key in a.Keys.Union(b.Keys,StringComparer.OrdinalIgnoreCase)
                .Union(c.Keys,StringComparer.OrdinalIgnoreCase))
            {
                a.TryGetValue(key,out string? own);
                b.TryGetValue(key,out string? old);
                c.TryGetValue(key,out string? other);
                if(other is null && old is not null && own is not null)
                    throw new InvalidDataException("Peer record absence is not a deletion tombstone: "+group+"/"+key);
                if(own is null && old is not null && other is not null)
                    throw new InvalidDataException("Local record absence is not a deletion tombstone: "+group+"/"+key);
                if(own!=other && own!=old && other!=old)
                    throw new InvalidDataException("Conflicting edits need manual resolution: "+group+"/"+key);
                JsonNode? selected;
                if(own==old && other!=old)
                {
                    incomingCount++;
                    selected=other is null?null:remoteNodes[key];
                }
                else
                {
                    if(own!=old && own!=other)localCount++;
                    selected=own is null?null:localNodes[key];
                }
                if(selected is not null)merged.Add(key,selected.DeepClone());
            }
            // Keep local order for existing deck library and append new peer IDs.
            var output=new JsonArray();
            foreach(var row in (JsonArray)local[group]!)
            {
                string key=Key(group,row);
                if(merged.Remove(key,out JsonNode? value))output.Add(value);
            }
            foreach(var row in (JsonArray)remoteData[group]!)
            {
                string key=Key(group,row);
                if(merged.Remove(key,out JsonNode? value))output.Add(value);
            }
            if(merged.Count!=0)throw new InvalidDataException("Merge left unmatched records.");
            candidate[group]=output;
        }
        var localArt=(JsonObject)local["preferredArtwork"]!;
        var baseArt=(JsonObject)baseData["preferredArtwork"]!;
        var remoteArt=(JsonObject)remoteData["preferredArtwork"]!;
        var mergedArt=(JsonObject)localArt.DeepClone();
        foreach(string key in localArt.Select(x=>x.Key)
            .Union(baseArt.Select(x=>x.Key),StringComparer.OrdinalIgnoreCase)
            .Union(remoteArt.Select(x=>x.Key),StringComparer.OrdinalIgnoreCase))
        {
            bool hasLocal=localArt.TryGetPropertyValue(key,out var localValue);
            bool hasBase=baseArt.TryGetPropertyValue(key,out var baseValue);
            bool hasRemote=remoteArt.TryGetPropertyValue(key,out var remoteValue);
            if(hasBase && (!hasRemote || !hasLocal))
                throw new InvalidDataException("Artwork deletion requires an explicit tombstone: "+key);
            var own=hasLocal?Canonical(localValue):null;
            var old=hasBase?Canonical(baseValue):null;
            var other=hasRemote?Canonical(remoteValue):null;
            if(own!=other && own!=old && other!=old)
                throw new InvalidDataException("Conflicting artwork edits: "+key);
            if(own==old && other!=old)
            {
                if(!hasRemote)throw new InvalidDataException("Artwork deletion missing tombstone: "+key);
                mergedArt[key]=remoteValue?.DeepClone();incomingCount++;
            }
            else if(own!=old && own!=other)localCount++;
        }
        candidate["preferredArtwork"]=mergedArt;

        // Unknown or opaque workspace metadata must never silently be
        // overwritten. Keep the native local value and reject remote edits.
        foreach(var field in remoteData)
        {
            if(field.Key is "cards" or "physicalCards" or "deckLibrary" or "preferredArtwork" or
                "schema" or "source" or "exported")continue;
            string? old=baseData[field.Key] is null?null:Canonical(baseData[field.Key]);
            string? proposed=field.Value is null?null:Canonical(field.Value);
            if(old!=proposed && local[field.Key] is not null &&
                Canonical(local[field.Key])!=proposed)
                throw new InvalidDataException("Unknown workspace field changed on peer: "+field.Key);
        }
        byte[] proposedBytes=JsonSerializer.SerializeToUtf8Bytes(candidate);
        if(proposedBytes.Length>MaxBytes)
            throw new InvalidDataException("Merged snapshot exceeds 30 MiB.");
        // Validate every candidate field with the same reader as native import.
        NativeBackupReader.Read(proposedBytes);
        return new MergeProposal(Digest(localBytes),remote.Device,incomingCount,localCount,
            proposedBytes,incomingCount>0);
    }

    /// <summary>Call only after explicit user approval of PrepareMerge.
    /// A changed local SHA aborts. NativeWorkspaceSafety stages, verifies and
    /// retains the previous SQLite file as a rollback.</summary>
    public static NativeWorkspaceSafety.Replacement ApplyApprovedMerge(
        string nativePath,MergeProposal proposal,string expectedCurrentSha)
    {
        if(!proposal.HasChanges)throw new InvalidDataException("Nothing to apply.");
        if(!string.Equals(proposal.ExpectedCurrentSha,expectedCurrentSha,StringComparison.Ordinal))
            throw new InvalidDataException("Merge candidate changed since approval.");
        return NativeWorkspaceSafety.Refresh(nativePath,proposal.CandidateBackup,
            "peer-sync-v2-approved-three-way",expectedCurrentSha);
    }

    public static Preview Compare(string nativePath, byte[] incoming)
    {
        var peer = ParsePeer(incoming);
        byte[] current = ReadCurrent(nativePath);
        var local = RequireData(JsonNode.Parse(current));
        var changes = new List<Change>();
        int equal = 0;
        foreach (var group in new[] { "cards", "physicalCards", "deckLibrary" })
        {
            var a = Index(group, (JsonArray)local[group]!);
            var b = Index(group, (JsonArray)peer.Data[group]!);
            foreach (string key in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase))
            {
                a.TryGetValue(key, out var before);
                b.TryGetValue(key, out var after);
                if (before == after) { equal++; continue; }
                string kind = before is null ? "Add" :
                    after is null ? "Absent in peer" : "Different";
                changes.Add(new Change(group,key,kind,before,after));
            }
        }
        var localArt = (JsonObject)local["preferredArtwork"]!;
        var peerArt = (JsonObject)peer.Data["preferredArtwork"]!;
        foreach (string key in localArt.Select(k=>k.Key).Union(peerArt.Select(k=>k.Key),
            StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            bool hasBefore=localArt.TryGetPropertyValue(key,out var beforeNode);
            bool hasAfter=peerArt.TryGetPropertyValue(key,out var afterNode);
            var before=hasBefore?Canonical(beforeNode):null;
            var after=hasAfter?Canonical(afterNode):null;
            if (before == after) { equal++; continue; }
            changes.Add(new Change("preferredArtwork", key,
                !hasBefore ? "Add" : !hasAfter ? "Absent in peer" : "Different",
                before,after));
        }
        return new Preview(peer.Device,peer.Revision,Digest(current),changes,equal,
            changes.Count(c=>c.Action=="Different"||c.Action=="Absent in peer"));
    }
}
