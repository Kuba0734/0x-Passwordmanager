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
}
