namespace PasswordVault.Core.Models;

public class AppSettings
{
    public int AutoLockSeconds { get; set; } = 300;
    public int ClipboardClearSeconds { get; set; } = 15;
    public string Theme { get; set; } = "System";
    public int DefaultPasswordLength { get; set; } = 20;
    public bool UseSymbols { get; set; } = true;
    public bool UseUppercase { get; set; } = true;
    public bool UseDigits { get; set; } = true;
    public bool WindowsHelloEnabled { get; set; } = false;
}
