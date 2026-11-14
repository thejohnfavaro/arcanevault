using System.Security.Cryptography;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Explicitly confirmed local peer checkpoint, not cryptographic device pairing.
/// A checkpoint may only be recorded when current recognized record groups and
/// the incoming peer snapshot compare equal. Previous checkpoints are retained.
/// </summary>
public static class NativePeerCheckpoint
{
    private static string DirectoryPath => Path.Combine(NativeWorkspace.RootDirectory,"peer-checkpoints");
    private static string PathFor(string device)
    {
        if(!Guid.TryParse(device,out var id)||id==Guid.Empty)
            throw new InvalidDataException("Peer checkpoint requires a valid device ID.");
        return Path.Combine(DirectoryPath,id.ToString("D")+".json");
    }
    public static bool Exists(string device)=>File.Exists(PathFor(device));
    public static byte[] Load(string device)
    {
        var path=PathFor(device);
        var length=new FileInfo(path).Length;
        if(length is <=20 or > NativePeerSync.MaxBytes)
            throw new InvalidDataException("Stored peer baseline is invalid or exceeds 30 MiB.");
        return File.ReadAllBytes(path);
    }
    public static string SaveIdentical(string nativePath,byte[] remote)
    {
        var preview=NativePeerSync.Compare(nativePath,remote);
        if(preview.Device==NativePeerSync.DeviceId())
            throw new InvalidDataException("A device cannot checkpoint itself.");
        if(preview.HasChanges)
            throw new InvalidDataException("Peer records differ; cannot establish a common baseline.");
        Directory.CreateDirectory(DirectoryPath);
        string path=PathFor(preview.Device);
        string temp=path+".stage-"+Guid.NewGuid().ToString("N");
        try
        {
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                file.Write(remote);
                file.Flush(flushToDisk:true);
            }
            // Guard against source changing while checkpoint was prepared.
            if(NativePeerSync.Compare(nativePath,remote).HasChanges)
                throw new InvalidDataException("Native collection changed during baseline setup.");
            if(File.Exists(path))
            {
                string archive=path+".history-"+
                    DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+
                    Guid.NewGuid().ToString("N")[..8]+".json";
                File.Replace(temp,path,archive,ignoreMetadataErrors:false);
            }
            else File.Move(temp,path);
            return path;
        }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
}
