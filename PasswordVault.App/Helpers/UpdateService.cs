using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PasswordVault.Core.Services;

namespace PasswordVault.App.Helpers;

/// <summary>
/// Updates über GitHub Releases (nur Release-Build ohne MSIX; MSIX aktualisiert sich über das Paket).
/// Ablauf: neueste Version abfragen → ZIP laden → SHA-256 prüfen → entpacken → apply.cmd ersetzt die Dateien
/// nach dem Beenden der App und startet sie neu.
/// </summary>
public static class UpdateService
{
    // Muss zum Release-Skript passen (deploy.ps1: $Repo, $ZipName)
    public const string Repo = "Kuba0734/0x-Passwordmanager";
    public const string ZipName = "PasswordVault-win-x64.zip";
    private const string ExeName = "PasswordVault.App.exe";

    public sealed record Release(Version Version, Uri ZipUrl, Uri ShaUrl);

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PasswordVault-Updater");
        return http;
    }

    public static bool IsSupported =>
#if DEBUG
        false;
#else
        !AutostartService.IsPackaged;
#endif

    /// <summary>Version dieser App (dreistellig, wie die Git-Tags).</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    private static string WorkDir => Path.Combine(Path.GetTempPath(), "PasswordVault-Update");

    /// <summary>Neueres Release oder null, wenn die App aktuell ist. Wirft bei Netzwerk-/Formatfehlern.</summary>
    public static async Task<Release?> CheckAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var tag = json.RootElement.GetProperty("tag_name").GetString() ?? "";
        var latest = UpdateFormat.ParseTag(tag) ?? throw new InvalidDataException($"Unbekanntes Versionsformat: {tag}");
        if (latest <= CurrentVersion) return null;

        Uri? zip = null, sha = null;
        foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if (name == ZipName) zip = url;
            if (name == ZipName + ".sha256") sha = url;
        }
        if (zip == null || sha == null)
            throw new InvalidDataException($"Im Release {tag} fehlt {ZipName} oder die Prüfsumme.");
        return new Release(latest, zip, sha);
    }

    /// <summary>
    /// Lädt und prüft das Update. Gibt den Ordner mit den neuen Programmdateien zurück.
    /// Stimmt die Prüfsumme nicht oder fehlt die exe, wird alles verworfen und eine Ausnahme geworfen.
    /// </summary>
    public static async Task<string> DownloadAsync(Release release)
    {
        CleanUp();
        var dir = Directory.CreateDirectory(Path.Combine(WorkDir, release.Version.ToString())).FullName;
        var zipPath = Path.Combine(dir, ZipName);
        try
        {
            var expected = UpdateFormat.ParseSha256(await Http.GetStringAsync(release.ShaUrl));
            await using (var source = await Http.GetStreamAsync(release.ZipUrl))
            await using (var target = File.Create(zipPath))
                await source.CopyToAsync(target);

            string actual;
            await using (var file = File.OpenRead(zipPath))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Die Prüfsumme des Updates stimmt nicht – Update verworfen.");

            // ExtractToDirectory lehnt Einträge ab, die aus dem Zielordner herausführen
            var files = Path.Combine(dir, "files");
            ZipFile.ExtractToDirectory(zipPath, files);
            if (!File.Exists(Path.Combine(files, ExeName)))
                throw new InvalidDataException($"Das Update enthält keine {ExeName} – Update verworfen.");
            return files;
        }
        catch
        {
            CleanUp();
            throw;
        }
    }

    /// <summary>
    /// Startet versteckt ein Skript, das auf das Ende dieses Prozesses wartet, die neuen Dateien
    /// über die Installation kopiert und die App neu startet. Der Aufrufer beendet danach die App.
    /// </summary>
    public static void StartApply(string newFiles, bool startInTray)
    {
        var target = AppContext.BaseDirectory.TrimEnd('\\');
        var exe = Path.Combine(target, ExeName);
        var pid = Environment.ProcessId;
        var script = Path.Combine(Directory.CreateDirectory(WorkDir).FullName, "apply.cmd");

        // ping statt timeout: timeout bricht ohne Konsoleneingabe (versteckt gestartet) sofort ab
        var cmd = $"""
            @echo off
            chcp 65001 >nul
            :wait
            tasklist /FI "PID eq {pid}" /NH 2>nul | find " {pid} " >nul
            if not errorlevel 1 (
              ping -n 2 127.0.0.1 >nul
              goto wait
            )
            robocopy "{newFiles.TrimEnd('\\')}" "{target}" /E /R:5 /W:1 /NFL /NDL /NJH /NJS /NP >nul
            start "" "{exe}" {(startInTray ? AutostartService.TrayArgument : "")}
            """;
        File.WriteAllText(script, cmd.ReplaceLineEndings("\r\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetTempPath()
        });
    }

    /// <summary>Reste eines früheren Updates entfernen (das ZIP allein hat ~90 MB).</summary>
    public static void CleanUp()
    {
        try
        {
            if (Directory.Exists(WorkDir)) Directory.Delete(WorkDir, recursive: true);
        }
        catch
        {
            // apply.cmd läuft evtl. noch oder Datei ist gesperrt – beim nächsten Mal erneut
        }
    }
}
