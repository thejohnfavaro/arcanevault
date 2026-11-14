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
        var invalid=JsonNode.Parse(exported)!.AsObject();
        invalid["format"]="untrusted-peer";
        Reject(()=>NativePeerSync.Compare(path,
            JsonSerializer.SerializeToUtf8Bytes(invalid)));
        var duplicate=JsonNode.Parse(exported)!.AsObject();
        duplicate["data"]!["deckLibrary"]!.AsArray().Add(
            duplicate["data"]!["deckLibrary"]!.AsArray()[0]!.DeepClone());
        Reject(()=>NativePeerSync.Compare(path,
            JsonSerializer.SerializeToUtf8Bytes(duplicate)));
        Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))),
            "Dry-run preview altered the source native workspace.");
    }
}
