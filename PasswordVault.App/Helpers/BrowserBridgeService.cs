using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WinCredVault = Windows.Security.Credentials.PasswordVault;
using WinCredential = Windows.Security.Credentials.PasswordCredential;

namespace PasswordVault.App.Helpers;

/// <summary>
/// Lokale HTTP-Schnittstelle für die PasswordVault-Browsererweiterung.
///
/// Sicherheit:
///  - lauscht nur auf localhost
///  - Anfragen von normalen Webseiten (http/https-Origin) werden abgewiesen
///  - jede Anfrage außer /api/ping und /api/pair braucht ein Kopplungs-Token
///    (Header X-Vault-Token), das der Benutzer in der App bestätigt hat
///  - Passwörter werden nur bei entsperrtem Tresor und nur einzeln herausgegeben
///
/// Endpunkte (alle JSON):
///   GET  /api/ping                  → { app, version, paired, unlocked? }
///   POST /api/pair    {code,browser}→ { token }   (Bestätigung in der App)
///   GET  /api/status                → { unlocked, count }
///   POST /api/unlock  {password}    → 200 | 401
///   POST /api/lock
///   GET  /api/entries?url=&amp;q=       → { entries: [ohne Passwörter] }
///   GET  /api/cards                 → { cards: [ohne Nummer/CVC, mit last4] }
///   GET  /api/entries/{id}          → vollständiger Eintrag
///   POST /api/entries               → { id }
///   PUT  /api/entries/{id}          → Teil-Update (z.B. neues Passwort)
///   GET  /api/generate              → { password }
/// </summary>
public sealed class BrowserBridgeService : IDisposable
{
    // Die Erweiterung erwartet 27832; der Override ist nur für Tests neben einer laufenden App gedacht
    public static readonly int Port =
        int.TryParse(Environment.GetEnvironmentVariable("PASSWORDVAULT_BRIDGE_PORT"), out var p) ? p : 27832;
    public const string Version = "2.0";

    private readonly VaultStore _store;
    private readonly SettingsService _settings;
    private readonly PasswordGenerator _generator = new();
    private readonly SemaphoreSlim _pairGate = new(1, 1);
    private HttpListener? _listener;
    private int _failedUnlocks;
    private DateTime _unlockBlockedUntil = DateTime.MinValue;

    public BrowserBridgeService(VaultStore store, SettingsService settings)
    {
        _store = store;
        _settings = settings;
    }

    public bool IsRunning { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastContact { get; private set; }
    public bool IsPaired => TokenStore.Load() != null;

    /// <summary>Wird von der UI gesetzt: zeigt den Kopplungsdialog und liefert die Entscheidung.</summary>
    public Func<string, string, Task<bool>>? PairingRequested { get; set; }

    public event EventHandler? StatusChanged;

    public void Start()
    {
        if (IsRunning) return;
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
            IsRunning = true;
            LastError = null;
            _ = Task.Run(ListenAsync);
        }
        catch (Exception ex)
        {
            IsRunning = false;
            LastError = ex is HttpListenerException { ErrorCode: 183 or 32 }
                ? $"Port {Port} ist bereits belegt (läuft die App doppelt?)."
                : ex.Message;
            _listener = null;
        }
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        IsRunning = false;
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
        _listener = null;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RevokePairing()
    {
        TokenStore.Remove();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ListenAsync()
    {
        var listener = _listener;
        while (IsRunning && listener is { IsListening: true })
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            var origin = req.Headers["Origin"];
            if (!IsAllowedOrigin(origin))
            {
                res.StatusCode = 403;
                return;
            }

            if (origin != null)
            {
                res.Headers["Access-Control-Allow-Origin"] = origin;
                res.Headers["Vary"] = "Origin";
                res.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, OPTIONS";
                res.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Vault-Token";
            }

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                return;
            }

            var path = (req.Url?.AbsolutePath ?? "/").TrimEnd('/');
            var method = req.HttpMethod;
            bool authorized = IsAuthorized(req);

            if (path == "/api/ping" && method == "GET")
            {
                var ping = new JsonObject
                {
                    ["app"] = "PasswordVault",
                    ["version"] = Version,
                    ["paired"] = authorized,
                    // Die Erweiterung lädt sich neu, wenn ihre Version davon abweicht
                    ["extension"] = ExtensionFolder.BundledVersion
                };
                if (authorized) ping["unlocked"] = _store.IsUnlocked;
                await WriteJson(res, 200, ping);
                return;
            }

            if (path == "/api/pair" && method == "POST")
            {
                await HandlePairAsync(req, res);
                return;
            }

            if (!authorized)
            {
                await WriteError(res, 401, "not_paired");
                return;
            }

            LastContact = DateTime.Now;

            switch (method, path)
            {
                case ("GET", "/api/status"):
                    await WriteJson(res, 200, new JsonObject
                    {
                        ["unlocked"] = _store.IsUnlocked,
                        ["count"] = _store.IsUnlocked ? _store.Count : 0
                    });
                    return;

                case ("POST", "/api/unlock"):
                    await HandleUnlockAsync(req, res);
                    return;

                case ("POST", "/api/lock"):
                    _store.Lock(VaultChangeSource.Browser);
                    await WriteJson(res, 200, new JsonObject { ["ok"] = true });
                    return;

                case ("GET", "/api/generate"):
                    var s = _settings.Current;
                    var pw = _generator.Generate(s.DefaultPasswordLength, s.UseLowercase,
                        s.UseUppercase, s.UseDigits, s.UseSymbols, s.ExcludeAmbiguous);
                    await WriteJson(res, 200, new JsonObject { ["password"] = pw });
                    return;
            }

            if (!_store.IsUnlocked)
            {
                await WriteError(res, 423, "locked");
                return;
            }

            if (path == "/api/entries" && method == "GET")
            {
                var url = req.QueryString["url"];
                var q = req.QueryString["q"]?.Trim();
                var source = string.IsNullOrEmpty(url) ? _store.GetEntries() : _store.FindForUrl(url);
                var filtered = source.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                    filtered = filtered.Where(e =>
                        e.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                        e.Username.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                        e.Url.Contains(q, StringComparison.CurrentCultureIgnoreCase));
                if (string.IsNullOrEmpty(url))
                    filtered = filtered
                        .OrderByDescending(e => e.IsFavorite)
                        .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase);

                var arr = new JsonArray();
                foreach (var e in filtered.Take(300)) arr.Add(Summary(e));
                await WriteJson(res, 200, new JsonObject { ["entries"] = arr });
                return;
            }

            if (path == "/api/cards" && method == "GET")
            {
                // Karten gehören zu keiner Website → immer alle, nur mit den letzten 4 Ziffern
                var arr = new JsonArray();
                foreach (var e in _store.GetEntries().Where(e => e.Category == Categories.Cards && e.Password.Length > 0))
                {
                    var card = Summary(e);
                    card["last4"] = e.Password.Length >= 4 ? e.Password[^4..] : e.Password;
                    card["expiry"] = e.Expiry;
                    arr.Add(card);
                }
                await WriteJson(res, 200, new JsonObject { ["cards"] = arr });
                return;
            }

            if (path == "/api/entries" && method == "POST")
            {
                var body = await ReadJson(req);
                var entry = new PasswordEntry
                {
                    Title = Str(body, "title"),
                    Username = Str(body, "username"),
                    Password = Str(body, "password"),
                    Url = Str(body, "url"),
                    Notes = Str(body, "notes"),
                    Category = Categories.All.Contains(Str(body, "category")) ? Str(body, "category") : Categories.Logins
                };
                if (string.IsNullOrWhiteSpace(entry.Title))
                    entry.Title = UrlMatcher.GetHost(entry.Url) ?? "Neuer Eintrag";
                _store.Add(entry, VaultChangeSource.Browser);
                await WriteJson(res, 201, new JsonObject { ["id"] = entry.Id });
                return;
            }

            if (path.StartsWith("/api/entries/"))
            {
                var id = Uri.UnescapeDataString(path["/api/entries/".Length..]);
                var entry = _store.Get(id);
                if (entry == null)
                {
                    await WriteError(res, 404, "not_found");
                    return;
                }

                if (method == "GET")
                {
                    var full = Summary(entry);
                    full["password"] = entry.Password;
                    full["notes"] = entry.Notes;
                    full["expiry"] = entry.Expiry;
                    full["cvc"] = entry.Cvc;
                    await WriteJson(res, 200, full);
                    return;
                }

                if (method == "PUT")
                {
                    var body = await ReadJson(req);
                    if (body?["password"] is JsonValue) entry.Password = Str(body, "password");
                    if (body?["username"] is JsonValue) entry.Username = Str(body, "username");
                    if (body?["title"] is JsonValue) entry.Title = Str(body, "title");
                    if (body?["url"] is JsonValue) entry.Url = Str(body, "url");
                    _store.Update(entry, VaultChangeSource.Browser);
                    await WriteJson(res, 200, new JsonObject { ["ok"] = true });
                    return;
                }
            }

            await WriteError(res, 404, "unknown_endpoint");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BrowserBridge] {ex}");
            try { await WriteError(res, 500, "internal_error"); } catch { }
        }
        finally
        {
            try { res.Close(); } catch { }
        }
    }

    private async Task HandlePairAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var body = await ReadJson(req);
        var code = Str(body, "code");
        var browser = Str(body, "browser");
        if (code.Length is < 4 or > 12 || PairingRequested == null)
        {
            await WriteError(res, 400, "bad_request");
            return;
        }

        // Nur eine Kopplungsanfrage gleichzeitig – verhindert Dialog-Spam
        if (!await _pairGate.WaitAsync(0))
        {
            await WriteError(res, 429, "pairing_in_progress");
            return;
        }

        try
        {
            var approved = await PairingRequested(code, string.IsNullOrWhiteSpace(browser) ? "Browser" : browser);
            if (!approved)
            {
                await WriteError(res, 403, "denied");
                return;
            }

            var token = TokenStore.Load() ?? TokenStore.Create();
            StatusChanged?.Invoke(this, EventArgs.Empty);
            await WriteJson(res, 200, new JsonObject { ["token"] = token });
        }
        finally
        {
            _pairGate.Release();
        }
    }

    private async Task HandleUnlockAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        if (_store.IsUnlocked)
        {
            await WriteJson(res, 200, new JsonObject { ["ok"] = true });
            return;
        }
        if (!_store.Exists)
        {
            await WriteError(res, 409, "no_vault");
            return;
        }
        if (DateTime.UtcNow < _unlockBlockedUntil)
        {
            await WriteError(res, 429, "too_many_attempts");
            return;
        }

        var body = await ReadJson(req);
        var password = Str(body, "password");
        try
        {
            _store.Unlock(password, VaultChangeSource.Browser);
            _failedUnlocks = 0;
            await WriteJson(res, 200, new JsonObject { ["ok"] = true });
        }
        catch (CryptographicException)
        {
            if (++_failedUnlocks >= 5)
            {
                _unlockBlockedUntil = DateTime.UtcNow.AddSeconds(30);
                _failedUnlocks = 0;
            }
            await WriteError(res, 401, "wrong_password");
        }
    }

    private static JsonObject Summary(PasswordEntry e) => new()
    {
        ["id"] = e.Id,
        ["title"] = e.Title,
        ["username"] = e.Username,
        ["url"] = e.Url,
        ["host"] = UrlMatcher.GetHost(e.Url) ?? "",
        ["category"] = e.Category,
        ["favorite"] = e.IsFavorite,
        ["hasPassword"] = !string.IsNullOrEmpty(e.Password)
    };

    private static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrEmpty(origin)) return true; // kein Browser-Kontext (z.B. curl)
        return origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)
            || origin.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase)
            || origin.StartsWith("extension://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthorized(HttpListenerRequest req)
    {
        var provided = req.Headers["X-Vault-Token"];
        var expected = TokenStore.Load();
        if (string.IsNullOrEmpty(provided) || expected == null) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
    }

    private static async Task<JsonNode?> ReadJson(HttpListenerRequest req)
    {
        if (!req.HasEntityBody) return null;
        if (req.ContentLength64 > 1_000_000) throw new InvalidDataException("Body zu groß");
        using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        try { return JsonNode.Parse(text); }
        catch (JsonException) { return null; }
    }

    private static string Str(JsonNode? node, string key)
    {
        try { return node?[key]?.GetValue<string>() ?? ""; }
        catch { return ""; }
    }

    private static async Task WriteJson(HttpListenerResponse res, int status, JsonNode body)
    {
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
        res.StatusCode = status;
        res.ContentType = "application/json; charset=utf-8";
        res.Headers["Cache-Control"] = "no-store";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private static Task WriteError(HttpListenerResponse res, int status, string error)
        => WriteJson(res, status, new JsonObject { ["error"] = error });

    public void Dispose() => Stop();

    /// <summary>
    /// Kopplungs-Token im Windows-Anmeldeinformationstresor (pro Benutzer verschlüsselt).
    /// </summary>
    private static class TokenStore
    {
        private const string Resource = "PasswordVault_BrowserBridge";
        private const string User = "ExtensionToken";
        private static string? _cached;
        private static bool _loaded;

        public static string? Load()
        {
            if (_loaded) return _cached;
            try
            {
                var credential = new WinCredVault().Retrieve(Resource, User);
                credential.RetrievePassword();
                _cached = credential.Password;
            }
            catch
            {
                _cached = null;
            }
            _loaded = true;
            return _cached;
        }

        public static string Create()
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Remove();
            new WinCredVault().Add(new WinCredential(Resource, User, token));
            _cached = token;
            _loaded = true;
            return token;
        }

        public static void Remove()
        {
            try
            {
                var vault = new WinCredVault();
                vault.Remove(vault.Retrieve(Resource, User));
            }
            catch
            {
                // nichts gespeichert
            }
            _cached = null;
            _loaded = true;
        }
    }
}
