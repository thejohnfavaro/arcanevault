using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneVault.Next.Desktop;

/// <summary>Ephemeral, preview-only Wi-Fi exchange. No remote write access.
/// Uses a high-entropy bearer token over HTTP on a trusted local network.
/// Do not use on public/untrusted Wi-Fi; a future pairing protocol needs TLS.</summary>
internal sealed class WirelessPeerOffer : IDisposable
{
    public const int MaxBytes=30*1024*1024;
    private readonly object gate=new();
    private TcpListener? listener;
    private CancellationTokenSource? stop;
    private byte[]? payload;
    private Func<byte[],Task>? receive;
    private string? token;
    private DateTimeOffset expires;
    private bool busy;

    public sealed record Offer(string Url,int Seconds,string Direction);
    public Offer Share(byte[] snapshot)=>Start(snapshot,null);
    public Offer Request(Func<byte[],Task> handler)=>Start(null,handler);

    private Offer Start(byte[]? bytes,Func<byte[],Task>? handler)
    {
        if(bytes is {Length: < 20 or > MaxBytes})
            throw new InvalidDataException("Peer snapshot exceeds 30 MiB or is empty.");
        var address=LocalAddress()??throw new InvalidOperationException(
            "Connect both devices to trusted home Wi-Fi/Ethernet. No private IPv4 network found.");
        lock(gate)
        {
            StopLocked();
            stop=new CancellationTokenSource();
            listener=new TcpListener(IPAddress.Any,0);
            listener.Start(2);
            payload=bytes?.ToArray();
            receive=handler;
            token=Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            expires=DateTimeOffset.UtcNow.AddMinutes(5);
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            TcpListener active=listener;
            CancellationToken cancel=stop.Token;
            _=ServeAsync(active,cancel);
            _=ExpireAsync(active,cancel);
            return new Offer($"http://{address}:{port}/arcane-peer/{token}",300,
                handler is null?"Windows to Android":"Android to Windows");
        }
    }

    private async Task ExpireAsync(TcpListener active,CancellationToken ct)
    {
        try{await Task.Delay(TimeSpan.FromMinutes(5),ct);}
        catch(OperationCanceledException){return;}
        lock(gate){if(ReferenceEquals(listener,active))StopLocked();}
    }
    private async Task ServeAsync(TcpListener active,CancellationToken ct)
    {
        try
        {
            while(!ct.IsCancellationRequested)
            {
                TcpClient connection=await active.AcceptTcpClientAsync(ct);
                _=HandleAsync(connection,ct);
            }
        }
        catch(Exception e) when(e is SocketException or ObjectDisposedException or OperationCanceledException){}
    }
    private static async Task<string?> ReadLine(Stream stream,int limit,CancellationToken ct)
    {
        using var bytes=new MemoryStream();
        var single=new byte[1];
        while(bytes.Length<limit)
        {
            int n=await stream.ReadAsync(single,ct);
            if(n==0)return null;
            if(single[0]=='\n')return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            bytes.WriteByte(single[0]);
        }
        throw new InvalidDataException("Request header too long.");
    }
    private static async Task Reply(Stream stream,int status,byte[] bytes,CancellationToken ct)
    {
        string statusText=status==200?"OK":status==413?"Payload Too Large":"Not Found";
        var header=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {statusText}\r\n"+
            "Content-Type: application/json; charset=utf-8\r\n"+
            $"Content-Length: {bytes.Length}\r\nCache-Control: no-store\r\n"+
            "X-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header,ct);
        await stream.WriteAsync(bytes,ct);
        await stream.FlushAsync(ct);
    }
    private async Task HandleAsync(TcpClient client,CancellationToken ct)
    {
        using(client)
        {
            client.ReceiveTimeout=9000;
            client.SendTimeout=15000;
            bool claimed=false;
            try
            {
                using var stream=client.GetStream();
                string? request=await ReadLine(stream,1024,ct);
                if(request==null)return;
                long contentLength=-1;
                int headers=0;
                while(true)
                {
                    string? line=await ReadLine(stream,2048,ct);
                    if(line==null)return;
                    headers+=line.Length+2;
                    if(headers>8192)return;
                    if(line.Length==0)break;
                    if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase) &&
                        !long.TryParse(line.AsSpan(15).Trim(),out contentLength))return;
                }
                byte[]? outgoing;
                Func<byte[],Task>? callback;
                lock(gate)
                {
                    if(busy||token==null||DateTimeOffset.UtcNow>=expires||
                        request!=$"{(receive is null?"GET":"POST")} /arcane-peer/{token} HTTP/1.1")return;
                    claimed=true;busy=true;
                    outgoing=payload;callback=receive;
                }
                if(callback is null && outgoing!=null)
                {
                    await Reply(stream,200,outgoing,ct);
                }
                else if(callback!=null && contentLength is >=20 and <=MaxBytes)
                {
                    byte[] bytes=new byte[(int)contentLength];
                    await stream.ReadExactlyAsync(bytes,ct);
                    // Callback must perform strict peer validation and show review
                    // only. Never invoke inventory or deck mutation from the listener.
                    await callback(bytes);
                    await Reply(stream,200,Encoding.UTF8.GetBytes("{\"received\":true}"),ct);
                }
                else
                {
                    await Reply(stream,413,Encoding.UTF8.GetBytes("{\"error\":\"invalid size\"}"),ct);
                    return;
                }
                lock(gate)StopLocked();
            }
            catch(Exception e) when(e is IOException or SocketException or OperationCanceledException or InvalidDataException or ObjectDisposedException){}
            finally{if(claimed){lock(gate)busy=false;}}
        }
    }
    private static IPAddress? LocalAddress()
    {
        foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if(adapter.OperationalStatus!=OperationalStatus.Up||
                adapter.NetworkInterfaceType is not (NetworkInterfaceType.Wireless80211 or
                    NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or
                    NetworkInterfaceType.FastEthernetFx))continue;
            foreach(var addr in adapter.GetIPProperties().UnicastAddresses)
            {
                IPAddress ip=addr.Address;
                if(ip.AddressFamily!=AddressFamily.InterNetwork)continue;
                byte[] b=ip.GetAddressBytes();
                if(b[0]==10||(b[0]==172&&b[1] is >=16 and <=31)||
                    (b[0]==192&&b[1]==168))return ip;
            }
        }
        return null;
    }
    private void StopLocked()
    {
        try{stop?.Cancel();listener?.Stop();}catch{}
        stop?.Dispose();stop=null;listener=null;
        token=null;payload=null;receive=null;busy=false;
    }
    public void Cancel(){lock(gate)StopLocked();}
    public void Dispose()=>Cancel();
}
