namespace PasswordVault.Core.Models;

public class AppSettings
{
    public int AutoLockSeconds { get; set; } = 300;
    public int ClipboardClearSeconds { get; set; } = 15;
    public string Theme { get; set; } = "System";
    public bool AnimationsEnabled { get; set; } = true;

    public int DefaultPasswordLength { get; set; } = 20;
    public bool UseSymbols { get; set; } = true;
    public bool UseUppercase { get; set; } = true;
    public bool UseLowercase { get; set; } = true;
    public bool UseDigits { get; set; } = true;
    public bool ExcludeAmbiguous { get; set; } = false;

    public bool WindowsHelloEnabled { get; set; } = false;

    public bool BrowserBridgeEnabled { get; set; } = true;

    public bool MinimizeToTray { get; set; } = true;
    public bool TrayHintShown { get; set; } = false;

    // Vom Updater vor dem Neustart gesetzt, nach dem Neustart einmal als „Was ist neu“ angezeigt
    public string WhatsNewVersion { get; set; } = string.Empty;
    public string WhatsNewNotes { get; set; } = string.Empty;
}
