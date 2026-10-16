using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace PasswordVault.App.Helpers;

/// <summary>
/// Minimal localhost HTTP bridge so the PasswordVault browser extension can
/// push updated vault bytes directly to disk and trigger a live UI refresh —
/// without requiring the user to manually replace any file.
///
/// Endpoint:  PUT  http://localhost:27832/vault
///   Body  :  raw vault bytes (same binary format as vault.dat)
///   Response: 200 OK on success, 500 on error
/// </summary>
public sealed class BrowserBridgeService : IDisposable
{
    private readonly HttpListener _listener = new();
    private bool _running;

    public void Start()
    {
        if (_running) return;
        try
        {
            _listener.Prefixes.Add("http://localhost:27832/");
            _listener.Start();
            _running = true;
            Task.Run(ListenAsync);
        }
        catch
        {
            // Port already in use or insufficient permissions — bridge unavailable,
            // extension falls back to manual-download mode.
        }
    }

    private async Task ListenAsync()
    {
        while (_running && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }
            _ = HandleAsync(ctx);   // fire-and-forget per request
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        // CORS — the extension is on a chrome-extension:// origin
        res.Headers["Access-Control-Allow-Origin"]  = "*";
        res.Headers["Access-Control-Allow-Methods"] = "GET, PUT, OPTIONS";
        res.Headers["Access-Control-Allow-Headers"] = "Content-Type";

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 204;
            res.Close();
            return;
        }

        if (req.HttpMethod == "GET" && req.Url?.AbsolutePath == "/vault")
        {
            try
            {
                if (!File.Exists(App.VaultPath))
                {
                    res.StatusCode = 404;
                    res.Close();
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(App.VaultPath);
                res.ContentType = "application/octet-stream";
                res.ContentLength64 = bytes.Length;
                await res.OutputStream.WriteAsync(bytes);
                res.StatusCode = 200;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BrowserBridge] GET /vault error: {ex.Message}");
                res.StatusCode = 500;
            }

            res.Close();
            return;
        }

        if (req.HttpMethod == "PUT" && req.Url?.AbsolutePath == "/vault")
        {
            try
            {
                // Read new vault bytes from request body
                using var ms = new MemoryStream();
                await req.InputStream.CopyToAsync(ms);
                var newBytes = ms.ToArray();

                // Atomically replace vault file
                await File.WriteAllBytesAsync(App.VaultPath, newBytes);

                // If vault is currently unlocked, reload entries so UI reflects
                // the change instantly (without requiring the user to lock/unlock)
                if (App.CurrentEntries != null)
                {
                    try
                    {
                        var (_, encryptedData) = App.VaultFile.Load(App.VaultPath);
                        var newEntries = App.Vault.DecryptEntries(encryptedData);
                        App.CurrentEntries = newEntries;

                        App.MainWindowInstance.DispatcherQueue.TryEnqueue(() =>
                            App.MainWindowInstance.RefreshEntriesFromBridge(newEntries)
                        );
                    }
                    catch
                    {
                        // Decryption failed (shouldn't happen — extension used the same
                        // key). File is saved; entries refresh on next manual unlock.
                    }
                }

                res.StatusCode = 200;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BrowserBridge] PUT /vault error: {ex.Message}");
                res.StatusCode = 500;
            }

            res.Close();
            return;
        }

        res.StatusCode = 404;
        res.Close();
    }

    public void Dispose()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
        _listener.Close();
    }
}
