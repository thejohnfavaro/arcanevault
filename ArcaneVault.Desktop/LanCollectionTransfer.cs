using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneVault.Desktop;

/// <summary>Short-lived, user-initiated LAN transfer for a collection sync snapshot.
/// The QR contains only a one-time local URL, never the inventory itself.</summary>
internal sealed class LanCollectionTransfer : IDisposable
{
    private readonly object guard = new();
    private TcpListener? listener;
    private byte[]? payload;
    private string? token;
    private DateTimeOffset expiry;
    private CancellationTokenSource? cancellation;
    private const int MaxBytes = 8 * 1024 * 1024;

    internal sealed record TransferOffer(string url, int port, int expiresSeconds, int sizeBytes, string network);
    public TransferOffer Start(byte[] json, bool viaUsb = false)
    {
        if (json.Length is < 20 or > MaxBytes) throw new ArgumentException("Sync file exceeds 8 MB or is empty.");
        var address = viaUsb ? IPAddress.Loopback : FindLanAddress() ?? throw new InvalidOperationException("No Wi-Fi/Ethernet IPv4 address found. Connect both devices to the same network.");
        lock (guard)
        {
            StopLocked();
            cancellation = new CancellationTokenSource();
            listener = new TcpListener(viaUsb ? IPAddress.Loopback : IPAddress.Any, 0);
            listener.Start(2);
            payload = json.ToArray();
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            expiry = DateTimeOffset.UtcNow.AddMinutes(5);
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var url = $"http://{address}:{port}/arcane-vault-sync/{token}";
            _ = ListenAsync(listener, cancellation.Token);
            _ = ExpireAsync(listener, cancellation.Token);
            return new TransferOffer(url, port, 300, payload.Length, viaUsb ? "USB (ADB)" : address.ToString());
        }
    }

    private async Task ExpireAsync(TcpListener active, CancellationToken cancel)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), cancel);
            lock (guard)
            {
                if (ReferenceEquals(listener, active)) StopLocked();
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task ListenAsync(TcpListener active, CancellationToken cancel)
    {
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                var client = await active.AcceptTcpClientAsync(cancel);
                _ = HandleAsync(client, cancel);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancel)
    {
        using (client)
        {
            client.ReceiveTimeout = 3500;
            client.SendTimeout = 12000;
            try
            {
                using var stream = client.GetStream();
                // Only the first line is relevant; do not parse or execute arbitrary requests.
                var lineBytes = new List<byte>(300);
                var buffer = new byte[1];
                while (lineBytes.Count < 320)
                {
                    int read = await stream.ReadAsync(buffer, cancel);
                    if (read == 0 || buffer[0] == (byte)'\n') break;
                    if (buffer[0] != (byte)'\r') lineBytes.Add(buffer[0]);
                }
                var line = Encoding.ASCII.GetString(lineBytes.ToArray());
                // HttpURLConnection sends HTTP request headers after the request line.
                // Closing a TCP socket with these bytes unread can send an RST that
                // truncates our JSON response ("Unexpected end of stream" on Android).
                // Consume the entire header section before replying, with a strict bound.
                int headerBytes = 0;
                bool endOfHeaders = false;
                int consecutiveNewlines = 0;
                while (headerBytes < 8192)
                {
                    int read = await stream.ReadAsync(buffer, cancel);
                    if (read == 0) break;
                    headerBytes++;
                    if (buffer[0] == (byte)'\n')
                    {
                        consecutiveNewlines++;
                        if (consecutiveNewlines >= 2) { endOfHeaders = true; break; }
                    }
                    else if (buffer[0] != (byte)'\r') consecutiveNewlines = 0;
                }
                if (!endOfHeaders) return;
                byte[]? content = null;
                lock (guard)
                {
                    if (DateTimeOffset.UtcNow < expiry && token is not null &&
                        line == $"GET /arcane-vault-sync/{token} HTTP/1.1")
                    {
                        // A single successful GET consumes the token; do not leave collection data open.
                        content = payload;
                        payload = null;
                        token = null;
                    }
                }
                if (content is null)
                {
                    await RespondAsync(stream, 404, "text/plain", Encoding.UTF8.GetBytes("Invalid, expired or already used transfer link."), cancel);
                    return;
                }
                await RespondAsync(stream, 200, "application/json", content, cancel);
                await stream.FlushAsync(cancel);
                // Finish sending before closing the socket and expiring the offer.
                client.Client.Shutdown(SocketShutdown.Send);
                lock (guard) { StopLocked(); }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        }
    }

    private static async Task RespondAsync(Stream stream, int status, string mime, byte[] data, CancellationToken cancel)
    {
        var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Not Found")}\r\nContent-Type: {mime}; charset=utf-8\r\nContent-Length: {data.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head, cancel);
        await stream.WriteAsync(data, cancel);
    }

    private static IPAddress? FindLanAddress()
    {
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up ||
                network.NetworkInterfaceType is not (NetworkInterfaceType.Wireless80211 or
                    NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or
                    NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT))
                continue;
            foreach (var address in network.GetIPProperties().UnicastAddresses)
            {
                var ip = address.Address;
                if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                var b = ip.GetAddressBytes();
                if (b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                    (b[0] == 192 && b[1] == 168)) return ip;
            }
        }
        return null;
    }

    private void StopLocked()
    {
        try { cancellation?.Cancel(); listener?.Stop(); } catch { }
        cancellation?.Dispose(); cancellation = null; listener = null;
        payload = null; token = null;
    }

    public void Cancel() { lock (guard) StopLocked(); }
    public void Dispose() { Cancel(); }
}
