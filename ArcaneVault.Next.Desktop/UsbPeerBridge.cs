using System.Diagnostics;
using System.IO;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Explicit, single-device ADB reverse bridge for a one-use, loopback-only
/// peer snapshot offer. ADB is used solely to carry the transport and start
/// the application; no ADB access is granted to the peer HTTP endpoint.
/// </summary>
internal static class UsbPeerBridge
{
    private static string FindAdb()
    {
        var candidates=new List<string>();
        var sdk=Environment.GetEnvironmentVariable("ANDROID_HOME") ??
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        if(!string.IsNullOrWhiteSpace(sdk))
            candidates.Add(Path.Combine(sdk,"platform-tools","adb.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),"Android","Sdk","platform-tools","adb.exe"));
        foreach(var part in (Environment.GetEnvironmentVariable("PATH")??"").Split(
            Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(part.Trim('"'),"adb.exe"));
        return candidates.FirstOrDefault(File.Exists) ??
            throw new FileNotFoundException(
                "ADB platform-tools not found. Install Android SDK Platform-Tools, "+
                "or add adb.exe to PATH. Enable USB debugging on your phone.");
    }

    private static async Task<string> ExecuteAsync(string adb,params string[] args)
    {
        using var process=new Process();
        process.StartInfo=new ProcessStartInfo(adb) {
            UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true
        };
        foreach(string arg in args)process.StartInfo.ArgumentList.Add(arg);
        if(!process.Start())throw new IOException("Could not start adb.exe");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try {
            var output=process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error=process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            string text=(await output)+"\n"+(await error);
            if(process.ExitCode!=0)throw new IOException(
                "ADB rejected USB sync: "+text.Trim());
            return text;
        } catch(OperationCanceledException) {
            try{process.Kill(entireProcessTree:true);}catch{}
            throw new TimeoutException("ADB did not respond; reconnect the USB cable.");
        }
    }

    /// <summary>Return the single authorized device; never choose one implicitly
    /// when more than one phone/emulator is connected.</summary>
    private static async Task<(string Adb,string Serial)> ConnectedAsync()
    {
        string adb=FindAdb();
        string devices=await ExecuteAsync(adb,"devices");
        var serials=devices.Split('\n',StringSplitOptions.RemoveEmptyEntries)
            .Select(s=>s.Trim().Split('\t'))
            .Where(s=>s.Length==2 && s[1]=="device")
            .Select(s=>s[0]).ToArray();
        if(serials.Length!=1)throw new InvalidOperationException(
            serials.Length==0?"No authorized USB Android device. Connect the phone, "+
            "enable USB debugging and approve its authorization prompt.":
            "More than one ADB device is attached. Disconnect the extras.");
        return (adb,serials[0]);
    }

    public static async Task<(string Adb,string Serial,int Port)> OpenAsync(
        string loopbackUrl,bool sendFromAndroid)
    {
        Uri url=new(loopbackUrl,UriKind.Absolute);
        if(url.Scheme!="http" || url.Host!="127.0.0.1" ||
            url.Port is <1 or >65535 ||
            !System.Text.RegularExpressions.Regex.IsMatch(url.AbsolutePath,
                "^/arcane-peer/[a-f0-9]{48}$"))
            throw new ArgumentException("USB offer must be loopback-only.");
        var (adb,serial)=await ConnectedAsync();
        int port=url.Port;
        await ExecuteAsync(adb,"-s",serial,"reverse",
            "tcp:"+port,"tcp:"+port);
        try {
            // The explicitly targeted activity receives the expiring token.
            // Android still requires the user's approval to transfer or merge.
            await ExecuteAsync(adb,"-s",serial,"shell","am","start",
                "-n","com.arcanevault.mobile/.MainActivity",
                "--activity-single-top",
                "--es","arcane_usb_url",loopbackUrl,
                "--es","arcane_usb_mode",sendFromAndroid?"send":"receive");
            return (adb,serial,port);
        } catch {
            await CloseAsync((adb,serial,port));
            throw;
        }
    }
    public static async Task CloseAsync((string Adb,string Serial,int Port)? bridge)
    {
        if(bridge is not { } active)return;
        try {
            await ExecuteAsync(active.Adb,"-s",active.Serial,"reverse",
                "--remove","tcp:"+active.Port);
        } catch { /* ADB may have disconnected; loopback offer still expires. */ }
    }
}
