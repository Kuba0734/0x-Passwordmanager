using System.Text.Json;
using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public class SettingsService
{
    private readonly string _filePath;
    public AppSettings Current { get; private set; } = new();

    public event EventHandler? Saved;

    public SettingsService(string settingsDirectory)
    {
        _filePath = Path.Combine(
            settingsDirectory, "settings.json");
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            Current = JsonSerializer.Deserialize(json, VaultJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Beschädigte Einstellungen nicht zum Absturz führen lassen
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = VaultJsonContext.Default };
        var json = JsonSerializer.Serialize(Current, options);
        File.WriteAllText(_filePath, json);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
