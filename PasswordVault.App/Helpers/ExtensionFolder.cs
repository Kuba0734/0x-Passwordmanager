using System;
using System.IO;
using System.Text.Json;

namespace PasswordVault.App.Helpers;

/// <summary>
/// Kopie der Browser-Erweiterung unter Dokumente, aus der der Browser sie als „entpackte Erweiterung“ lädt.
/// </summary>
public static class ExtensionFolder
{
    public static string Source => Path.Combine(AppContext.BaseDirectory, "BrowserExtension");

    public static string Target => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "PasswordVault Browser-Erweiterung");

    /// <summary>Version der mit der App ausgelieferten Erweiterung (aus manifest.json), sonst "".</summary>
    public static string BundledVersion { get; } = ReadVersion(Source);

    public static void Install()
    {
        if (!Directory.Exists(Source))
            throw new DirectoryNotFoundException("Die Erweiterungsdateien fehlen im App-Paket.");
        CopyDirectory(Source, Target);
    }

    /// <summary>
    /// Nach einem App-Update: eingerichtete Erweiterung auf den neuen Stand bringen.
    /// Gibt true zurück, wenn Dateien ersetzt wurden.
    /// </summary>
    public static bool SyncIfInstalled()
    {
        try
        {
            if (!Directory.Exists(Target) || BundledVersion.Length == 0) return false;
            if (ReadVersion(Target) == BundledVersion) return false;
            CopyDirectory(Source, Target);
            return true;
        }
        catch
        {
            return false; // Ordner gesperrt o.ä. – beim nächsten Start erneut
        }
    }

    private static string ReadVersion(string dir)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")));
            return json.RootElement.GetProperty("version").GetString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
