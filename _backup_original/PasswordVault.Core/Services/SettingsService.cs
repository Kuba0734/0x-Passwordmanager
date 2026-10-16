using System.Text.Json;
using PasswordVault.Core.Models;
using PasswordVault.Core;

namespace PasswordVault.Core.Services;

public class SettingsService
{
    private readonly string _filePath;
    public AppSettings Current { get; private set; } = new();

    public SettingsService(string settingsDirectory)
    {
        _filePath = Path.Combine(
            settingsDirectory, "settings.json");
        Load();
    }

    public void Load()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            Current = JsonSerializer.Deserialize(json, VaultJsonContext.Default.AppSettings) ?? new AppSettings();

        }
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = VaultJsonContext.Default };
        var json = JsonSerializer.Serialize(Current, options);
        File.WriteAllText(_filePath, json);
    }
}
