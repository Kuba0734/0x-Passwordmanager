using PasswordVault.Core.Services;

namespace PasswordVault.Mobile;

/// <summary>
/// Tresor-Datei zwischen PC und iPhone austauschen (über iCloud Drive, OneDrive, AirDrop …).
/// Die Datei ist verschlüsselt, der Austausch über die Cloud ist daher unkritisch.
/// </summary>
public static class VaultTransfer
{
    public static async Task<bool> ImportAsync(Page page)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Tresor-Datei (vault.dat) wählen" });
        if (file == null) return false;

        var temp = Path.Combine(FileSystem.CacheDirectory, "import.dat");
        try
        {
            await using (var source = await file.OpenReadAsync())
            await using (var target = File.Create(temp))
                await source.CopyToAsync(target);

            // Nur echte Tresor-Dateien annehmen, sonst wäre der bisherige Tresor weg
            var (metadata, data) = new VaultFileService().Load(temp);
            if (metadata.Salt.Length == 0 || data.Length < 28)
                throw new InvalidDataException();
        }
        catch (Exception)
        {
            File.Delete(temp);
            await page.DisplayAlertAsync("Keine Tresor-Datei",
                "Die gewählte Datei ist keine gültige Password-Vault-Datei. Wähle die Datei „vault.dat“ von deinem PC.", "OK");
            return false;
        }

        if (App.Store.Exists && !await page.DisplayAlertAsync("Tresor ersetzen?",
                "Der Tresor auf diesem iPhone wird durch die gewählte Datei ersetzt. Der bisherige bleibt als „vault.dat.bak“ erhalten.",
                "Ersetzen", "Abbrechen"))
        {
            File.Delete(temp);
            return false;
        }

        App.Store.Lock();
        if (File.Exists(App.VaultPath))
            File.Replace(temp, App.VaultPath, App.VaultPath + ".bak");
        else
            File.Move(temp, App.VaultPath);
        return true;
    }

    public static Task ExportAsync() => Share.Default.RequestAsync(new ShareFileRequest
    {
        Title = "Tresor sichern",
        File = new ShareFile(App.VaultPath)
    });

    private static readonly FilePickerFileType CsvTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.iOS] = new[] { "public.comma-separated-values-text", "public.plain-text" }
    });

    /// <summary>CSV aus Chrome, Edge, Firefox, Bitwarden, KeePass … übernehmen; Duplikate werden übersprungen.</summary>
    public static async Task<int> ImportCsvAsync(Page page)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "CSV-Datei wählen", FileTypes = CsvTypes });
        if (file == null) return 0;

        string csv;
        await using (var stream = await file.OpenReadAsync())
        using (var reader = new StreamReader(stream))
            csv = await reader.ReadToEndAsync();

        var imported = new CsvService().Import(csv);
        if (imported.Count == 0)
        {
            await page.DisplayAlertAsync("Keine Einträge gefunden",
                "Die Datei enthält keine erkennbaren Zugangsdaten. Unterstützt werden u. a. Exporte aus Chrome, Edge, Firefox, Bitwarden und KeePass.", "OK");
            return 0;
        }

        // Duplikate (gleicher Titel, Benutzer und Passwort) überspringen
        var existing = App.Store.GetEntries().Select(x => (x.Title, x.Username, x.Password)).ToHashSet();
        var fresh = imported.Where(x => !existing.Contains((x.Title, x.Username, x.Password))).ToList();
        if (fresh.Count == 0)
        {
            await page.DisplayAlertAsync("Bereits vorhanden", $"Alle {imported.Count} Einträge sind schon im Tresor.", "OK");
            return 0;
        }

        var message = $"{fresh.Count} neue Einträge gefunden.";
        if (imported.Count > fresh.Count) message += $" {imported.Count - fresh.Count} Duplikate werden übersprungen.";
        message += "

Tipp: Lösche die CSV-Datei danach – sie ist unverschlüsselt.";
        if (!await page.DisplayAlertAsync("Import bestätigen", message, "Importieren", "Abbrechen"))
            return 0;

        App.Store.AddRange(fresh);
        return fresh.Count;
    }

    private static string CsvExportPath => Path.Combine(FileSystem.CacheDirectory, $"passwoerter_{DateTime.Now:yyyy-MM-dd}.csv");

    public static async Task ExportCsvAsync(Page page)
    {
        if (!await page.DisplayAlertAsync("Unverschlüsselt exportieren?",
                "Die CSV-Datei enthält alle Passwörter im Klartext. Jeder mit Zugriff auf die Datei kann sie lesen. "
                + "Für Sicherungen ist „Tresor sichern“ die bessere Wahl.",
                "Trotzdem exportieren", "Abbrechen"))
            return;

        DeleteCsvExports();
        var path = CsvExportPath;
        await File.WriteAllTextAsync(path, new CsvService().Export(App.Store.GetEntries()), new System.Text.UTF8Encoding(true));
        await Share.Default.RequestAsync(new ShareFileRequest { Title = "Als CSV exportieren", File = new ShareFile(path, "text/csv") });
    }

    /// <summary>Klartext-Exporte nicht liegen lassen: beim Sperren und vor jedem neuen Export löschen.</summary>
    public static void DeleteCsvExports()
    {
        foreach (var file in Directory.EnumerateFiles(FileSystem.CacheDirectory, "passwoerter_*.csv"))
            File.Delete(file);
    }
}
