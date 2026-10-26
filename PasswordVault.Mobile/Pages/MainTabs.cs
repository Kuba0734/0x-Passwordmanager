namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Hauptnavigation wie die Seitenleiste am PC: Tresor, Sicherheit, Generator, Einstellungen.
/// </summary>
public class MainTabs : TabbedPage
{
    public MainTabs()
    {
        BarBackgroundColor = Ui.Bg;
        SelectedTabColor = Ui.Accent;
        UnselectedTabColor = Ui.Subtle;

        Children.Add(Tab(new VaultPage(), "Tresor", "tab_vault.png"));
        Children.Add(Tab(new SecurityPage(), "Sicherheit", "tab_security.png"));
        Children.Add(Tab(new GeneratorPage(), "Generator", "tab_generator.png"));
        Children.Add(Tab(new SettingsPage(), "Einstellungen", "tab_settings.png"));

        // Tab-Wechsel als Crossfade; die Leiste selbst reagiert sofort
        CurrentPageChanged += (_, _) =>
        {
            if (Ui.ReduceMotion || CurrentPage is not NavigationPage { CurrentPage: ContentPage { Content: { } content } }) return;
            content.AbortAnimation("FadeTo");
            content.Opacity = 0;
            _ = content.FadeToAsync(1, Ui.Normal, Ui.Ease);
        };
    }

    private static NavigationPage Tab(Page root, string title, string icon) => new(root)
    {
        Title = title,
        IconImageSource = icon,
        BarBackgroundColor = Ui.Bg,
        BarTextColor = Ui.Text
    };
}
