using PasswordVault.Core.Services;
using PasswordVault.Mobile.Pages;

namespace PasswordVault.Mobile;

public class App : Application
{
    // Documents-Ordner der App: über UIFileSharingEnabled in der Dateien-App sichtbar
    public static string VaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "vault.dat");

    public static VaultStore Store { get; } = new(VaultPath);

    private static Window? _window;

    public App()
    {
        Ui.ApplyStyles(Resources);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(new UnlockPage());
        _window.Stopped += (_, _) => OnBackground();
        _window.Resumed += (_, _) => OnForeground();
        return _window;
    }

    // Kurz zur anderen App wechseln (Passwort einfügen) soll nicht sofort sperren – Dauer in den Einstellungen
    private static DateTime _backgroundSince;

    private static void OnBackground()
    {
        _backgroundSince = DateTime.UtcNow;
        // Inhalt verstecken, bevor iOS das Vorschaubild für den App-Umschalter aufnimmt
        if (_window?.Page != null && Store.IsUnlocked) _window.Page.Opacity = 0;
    }

    private static void OnForeground()
    {
        if (DateTime.UtcNow - _backgroundSince >= TimeSpan.FromSeconds(MobileSettings.LockAfterSeconds)) Lock();
        if (_window?.Page != null) _window.Page.Opacity = 1;
    }

    public static void ShowVault() => SetPage(new MainTabs());

    public static void Lock()
    {
        if (Store.IsUnlocked) ShowUnlock();
    }

    public static void ShowUnlock()
    {
        Store.Lock();
        GeneratorPage.ClearHistory();
        VaultTransfer.DeleteCsvExports();
        SetPage(new UnlockPage());
    }

    private static void SetPage(Page page)
    {
        if (_window != null) _window.Page = page;
    }
}
