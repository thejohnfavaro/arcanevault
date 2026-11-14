using ArcaneVault.Next.Collection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class NativePeerSyncChecks
{
    private static void Check(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("Peer sync: "+message);
    }
    private static void Reject(Action action)
    {
        try{action();}
        catch(Exception ex) when(ex is InvalidDataException or JsonException){return;}
        throw new InvalidOperationException("Peer sync accepted an invalid file.");
    }
    public static void Run(string work)
    {
        string path=Path.Combine(work,"peer-fixture-native.sqlite");
        var raw=JsonSerializer.SerializeToUtf8Bytes(new {
            schema=2,
            cards=new object[]{new{name="Lightning Bolt",qty=4}},
            physicalCards=new object[]{new{name="Sol Ring",qty=2,
                set="CMM",number="397",finish="nonfoil",
                scryfallId="11111111-1111-4111-8111-111111111111"}},
            deckLibrary=new object[]{new{id="deck_1",name="Commander Sample",
                commander="Sample Commander",entries=new[]{new{name="Sol Ring",qty=1}}}},
            preferredArtwork=new Dictionary<string,object>{
                ["sol ring"]=new{image="https://cards.scryfall.io/test.jpg"}}
        });
        NativeWorkspace.ImportNewBytes(raw,"peer-sync-synthetic-fixture",path);
        byte[] before=SHA256.HashData(File.ReadAllBytes(path));
        byte[] exported=NativePeerSync.Export(path,
            "00000000-0000-4000-8000-000000000365");
        var same=NativePeerSync.Compare(path,exported);
        Check(same.Changes.Count==0 && same.EqualRecords==4,
            "Identical peer snapshot should be a no-op.");
        // A peer checkpoint can be stored only after identical recognized
        // records; previous checkpoints are retained instead of overwritten.
        string checkpointRoot=Path.Combine(work,"isolated-peer-checkpoints");
        var remoteBaseline=JsonNode.Parse(exported)!.AsObject();
        remoteBaseline["deviceId"]="00000000-0000-4000-8000-000000000366";
        byte[] peerBytes=JsonSerializer.SerializeToUtf8Bytes(remoteBaseline);
        var checkpoint=NativePeerCheckpoint.SaveIdentical(path,peerBytes,checkpointRoot);
        Check(File.Exists(checkpoint),"Matching peer checkpoint must persist.");
        Check(NativePeerCheckpoint.Exists("00000000-0000-4000-8000-000000000366",
            checkpointRoot),"Checkpoint should be discoverable by peer UUID.");
        Check(NativePeerCheckpoint.Load("00000000-0000-4000-8000-000000000366",
            checkpointRoot).SequenceEqual(peerBytes),"Persisted peer snapshot must remain lossless.");
        NativePeerCheckpoint.SaveIdentical(path,peerBytes,checkpointRoot);
        Check(Directory.GetFiles(checkpointRoot,"*.history-*.json").Length==1,
            "Refreshing a peer checkpoint must preserve one earlier snapshot.");
        var wrongPeer=JsonNode.Parse(peerBytes)!.AsObject();
        wrongPeer["data"]!["cards"]!.AsArray()[0]!["qty"]=99;
        Reject(()=>NativePeerCheckpoint.SaveIdentical(path,
            JsonSerializer.SerializeToUtf8Bytes(wrongPeer),checkpointRoot));
        var modified=JsonNode.Parse(exported)!.AsObject();
        var data=modified["data"]!.AsObject();
        data["cards"]!.AsArray()[0]!["qty"]=5;
        data["physicalCards"]!.AsArray().Add(JsonNode.Parse("""
            {"name":"Counterspell","qty":1,"set":"2XM","number":"47",
             "scryfallId":"22222222-2222-4222-8222-222222222222"}
            """));
        data["deckLibrary"]!.AsArray()[0]!["name"]="Remote renamed deck";
        var changed=NativePeerSync.Compare(path,
            JsonSerializer.SerializeToUtf8Bytes(modified));
        Check(changed.Additions==1 && changed.Updates==2 &&
            changed.Conflicts==2 && !changed.SafeToAutoApply,
            "Expected one new printing and two unresolved conflicts.");
        var ordered=JsonNode.Parse(exported)!.AsObject();
        ordered["data"]!["cards"]!.AsArray()[0]=JsonNode.Parse(
            """{"qty":4,"name":"Lightning Bolt"}""");
        var orderSafe=NativePeerSync.Compare(path,
            JsonSerializer.SerializeToUtf8Bytes(ordered));
        Check(!orderSafe.HasChanges && orderSafe.EqualRecords==4,
            "Android JSON field order must not trigger false Windows conflicts.");
        var baseExport=JsonNode.Parse(exported)!.AsObject();
        var remoteOnly=JsonNode.Parse(exported)!.AsObject();
        remoteOnly["data"]!["cards"]!.AsArray()[0]!["qty"]=5;
        var plan=NativePeerSync.CompareWithBaseline(path,
            JsonSerializer.SerializeToUtf8Bytes(remoteOnly),
            JsonSerializer.SerializeToUtf8Bytes(baseExport));
        Check(plan.IncomingOnly==1 && plan.LocalOnly==0 &&
            plan.Conflicts==0 && !plan.SafeToAutoApply,
            "Expected one incoming-only modification relative to the shared base.");
        var divergedBase=JsonNode.Parse(exported)!.AsObject();
        divergedBase["data"]!["cards"]!.AsArray()[0]!["qty"]=3;
        var divergent=NativePeerSync.CompareWithBaseline(path,
            JsonSerializer.SerializeToUtf8Bytes(remoteOnly),
            JsonSerializer.SerializeToUtf8Bytes(divergedBase));
        Check(divergent.Conflicts==1 && !divergent.SafeToAutoApply,
            "Independently modified records must be treated as conflicts.");
        var invalid=JsonNode.Parse(exported)!.AsObject();
        invalid["format"]="untrusted-peer";
        Reject(()=>NativePeerSync.Compare(path,
            JsonSerializer.SerializeToUtf8Bytes(invalid)));
        var duplicate=JsonNode.Parse(exported)!.AsObject();
        duplicate["data"]!["deckLibrary"]!.AsArray().Add(
            duplicate["data"]!["deckLibrary"]!.AsArray()[0]!.DeepClone());
        Reject(()=>NativePeerSync.Compare(path,
            JsonSerializer.SerializeToUtf8Bytes(duplicate)));
        // Work in a separate workspace because apply must actually replace the
        // collection while preserving a recoverable copy of the original.
        string writablePath=Path.Combine(work,"peer-apply-fixture.sqlite");
        NativeWorkspace.ImportNewBytes(raw,"peer-sync-apply-fixture",writablePath);
        byte[] baseBytes=NativePeerSync.Export(writablePath,
            "00000000-0000-4000-8000-000000000365");
        var received=JsonNode.Parse(baseBytes)!.AsObject();
        received["deviceId"]="00000000-0000-4000-8000-000000000999";
        received["data"]!["cards"]!.AsArray()[0]!["qty"]=6;
        received["data"]!["physicalCards"]!.AsArray().Add(JsonNode.Parse("""
            {"name":"Counterspell","qty":1,"set":"2XM","number":"47",
             "scryfallId":"22222222-2222-4222-8222-222222222222"}
            """));
        byte[] incomingBytes=JsonSerializer.SerializeToUtf8Bytes(received);
        var prepared=NativePeerSync.PrepareMerge(writablePath,incomingBytes,baseBytes);
        Check(prepared.IncomingChanges==2 && prepared.LocalChanges==0 &&
            prepared.HasChanges,"Clean peer additions should prepare two changes.");
        string previousSha=NativeWorkspace.Open(writablePath).BackupSha256;
        Reject(()=>NativePeerSync.ApplyApprovedMerge(writablePath,prepared,"wrong-sha"));
        var replacement=NativePeerSync.ApplyApprovedMerge(writablePath,prepared,previousSha);
        Check(File.Exists(replacement.RecoveryPath),
            "Approved merge must retain the original workspace as a recovery file.");
        Check(NativeWorkspace.Open(replacement.RecoveryPath).BackupSha256==previousSha,
            "Recovery file must preserve the original verified SHA.");
        var now=NativePeerSync.Compare(writablePath,incomingBytes);
        Check(!now.HasChanges,"Applied local data must match incoming peer record groups.");
        Reject(()=>NativePeerSync.ApplyApprovedMerge(writablePath,prepared,previousSha));
        var damaged=JsonNode.Parse(baseBytes)!.AsObject();
        damaged["deviceId"]="00000000-0000-4000-8000-000000000999";
        damaged["data"]!["cards"]!.AsArray().Clear();
        Reject(()=>NativePeerSync.PrepareMerge(path,
            JsonSerializer.SerializeToUtf8Bytes(damaged),baseBytes));
        var competing=JsonNode.Parse(baseBytes)!.AsObject();
        competing["deviceId"]="00000000-0000-4000-8000-000000000999";
        competing["data"]!["cards"]!.AsArray()[0]!["qty"]=7;
        var wrongAncestor=JsonNode.Parse(baseBytes)!.AsObject();
        wrongAncestor["data"]!["cards"]!.AsArray()[0]!["qty"]=2;
        Reject(()=>NativePeerSync.PrepareMerge(path,
            JsonSerializer.SerializeToUtf8Bytes(competing),
            JsonSerializer.SerializeToUtf8Bytes(wrongAncestor)));
        Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))),
            "Dry-run preview altered the source native workspace.");
    }
}
