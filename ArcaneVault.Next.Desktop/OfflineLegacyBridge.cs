using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// ONE-TIME bridge for an installed legacy Arcane Vault profile. Creates an
/// isolated copy of Chromium Local Storage; a disposable WebView2 instance
/// reads only this copy at the exact local origin the old app used.
/// Neither legacy application processes nor profile files are changed.
/// Once imported, the persistent native app requires no browser storage.
/// </summary>
internal static class OfflineLegacyBridge
{
    private const string LocalOrigin = "http://127.0.0.1:8765";
    private const string LocalUrl = LocalOrigin + "/__arcane_vault_offline_migration";
    private const string StorageScript = """
        (() => {
            if (location.origin !== 'http://127.0.0.1:8765') return null;
            const keys = {};
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key !== null) keys[key] = localStorage.getItem(key);
            }
            return JSON.stringify(keys);
        })()
        """;

    internal sealed record Extracted(byte[] BackupBytes, NativeBackupView Collection);

    public static async Task<Extracted> ExtractAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        // Binding this specific existing app origin fails when the legacy
        // process/server is active. Never attach to the original running page.
        using var listener = new HttpListener();
        listener.Prefixes.Add(LocalOrigin + "/");
        try { listener.Start(); }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                "Close the existing Arcane Vault application first. " +
                "Its localhost port 8765 must be unused to safely read an isolated profile.", ex);
        }

        using var stop = new CancellationTokenSource();
        Task server = Task.Run(async () =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(
                "<!doctype html><html><head><meta charset='utf-8'></head>" +
                "<body>Arcane Vault local migration — isolated profile snapshot</body></html>");
            while (listener.IsListening && !stop.IsCancellationRequested)
            {
                HttpListenerContext request;
                try { request = await listener.GetContextAsync().WaitAsync(stop.Token); }
                catch (OperationCanceledException) { break; }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }

                try
                {
                    bool allowed = request.Request.HttpMethod == "GET" &&
                                   request.Request.Url?.AbsolutePath ==
                                       "/__arcane_vault_offline_migration" &&
                                   IPAddress.IsLoopback(request.Request.RemoteEndPoint?.Address ??
                                       IPAddress.None);
                    request.Response.StatusCode = allowed ? 200 : 404;
                    request.Response.ContentType = "text/html; charset=utf-8";
                    request.Response.Headers.Add("Cache-Control", "no-store");
                    if (allowed)
                    {
                        request.Response.ContentLength64 = bytes.Length;
                        await request.Response.OutputStream.WriteAsync(bytes);
                    }
                }
                catch (HttpListenerException) { }
                catch (IOException) { }
                finally
                {
                    try { request.Response.Close(); } catch (ObjectDisposedException) { }
                }
            }
        });

        string scratch = Path.Combine(Path.GetTempPath(),
            "arcane-vault-next-offline-" + Guid.NewGuid().ToString("N"));
        Window? popup = null;
        WebView2? browser = null;
        try
        {
            // No source write access at any point; the clone's WebView2 engine
            // may update its own TEMPORARY copy, never the installed profile.
            await Task.Run(() => LegacyProfileSnapshot.CopyToTemporaryProfile(
                LegacyProfileSnapshot.LiveProfilePath, scratch));

            popup = new Window
            {
                Title = "Reading isolated Arcane Vault data",
                Owner = owner,
                Width = 16, Height = 16,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Opacity = 0.01,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            browser = new WebView2();
            popup.Content = browser;
            popup.Show();

            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: scratch);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            var nav = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void Finished(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
                nav.TrySetResult(e.IsSuccess);
            browser.CoreWebView2.NavigationCompleted += Finished;
            try
            {
                browser.CoreWebView2.Navigate(LocalUrl);
                if (!await nav.Task.WaitAsync(TimeSpan.FromSeconds(25)))
                    throw new InvalidDataException(
                        "The isolated legacy WebView could not navigate to its original localhost origin.");
            }
            finally { browser.CoreWebView2.NavigationCompleted -= Finished; }

            if (!Uri.TryCreate(browser.Source?.ToString(), UriKind.Absolute, out var uri) ||
                uri.GetLeftPart(UriPartial.Authority) != LocalOrigin)
                throw new InvalidDataException("Legacy profile snapshot loaded an unexpected origin.");

            string result = await browser.ExecuteScriptAsync(StorageScript)
                .WaitAsync(TimeSpan.FromSeconds(25));
            string? json = JsonSerializer.Deserialize<string>(result);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException(
                    "No recoverable legacy storage at the expected Windows origin. " +
                    "The native workspace was not created.");

            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Unexpected legacy local storage payload.");

            var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var field in parsed.RootElement.EnumerateObject())
            {
                if (field.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    throw new InvalidDataException("Invalid browser storage entry.");
                fields.Add(field.Name, field.Value.ValueKind == JsonValueKind.Null
                    ? null : field.Value.GetString());
            }
            byte[] backup = LegacyStorageImport.BuildFullBackup(fields);
            var view = NativeBackupReader.Read(backup);
            return new Extracted(backup, view);
        }
        finally
        {
            try { browser?.Dispose(); } catch { /* no source file touched */ }
            try { popup?.Close(); } catch { /* cleanup below */ }
            stop.Cancel();
            try { listener.Stop(); } catch (HttpListenerException) { }
            try { await server.WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
                    break;
                }
                catch (IOException) { if (attempt < 3) await Task.Delay(250); }
                catch (UnauthorizedAccessException) { if (attempt < 3) await Task.Delay(250); }
            }
        }
    }
}
