using PasswordVault.Core.Models;

namespace PasswordVault.Mobile.Pages;

/// <summary>
/// Details eines Eintrags: alles kopierbar, Passwort ein-/ausblendbar, Website öffnen.
/// </summary>
public class EntryPage : ContentPage
{
    private readonly string _id;
    private PasswordEntry? _entry;
    private bool _showPassword;

    public EntryPage(string id)
    {
        _id = id;
        ToolbarItems.Add(new ToolbarItem("Bearbeiten", null, async () =>
        {
            if (_entry != null) await Ui.Push(Navigation, new EditEntryPage(_entry));
        }));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!App.Store.IsUnlocked) return;

        // Nach dem Bearbeiten neu laden; gelöscht → zurück zur Liste
        _entry = App.Store.Get(_id);
        if (_entry == null)
        {
            Navigation.PopAsync();
            return;
        }
        Build(_entry);
    }

    private void Build(PasswordEntry e)
    {
        Title = e.Title;
        var views = new List<View>();

        var header = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(0, 10, 0, 6),
            Children =
            {
                Ui.Avatar(e.Title, 76),
                new Label { Text = e.Title, FontSize = 26, FontFamily = Ui.FontBold, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = (e.IsFavorite ? "Favorit · " : "") + e.Category, TextColor = Ui.Muted, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center }
            }
        };
        ((View)header.Children[0]).HorizontalOptions = LayoutOptions.Center;
        views.Add(header);

        var favorite = Ui.SecondaryButton(e.IsFavorite ? "Aus Favoriten entfernen" : "Zu Favoriten");
        favorite.Clicked += async (_, _) => await ToggleFavoriteAsync(favorite);
        views.Add(favorite);

        bool card = e.Category == Categories.Cards;
        var userCaption = card ? "Karteninhaber" : "Benutzername";
        var pwCaption = card ? "Kartennummer" : "Passwort";

        if (!string.IsNullOrEmpty(e.Username))
            views.Add(Ui.Field(userCaption, Value(e.Username),
                Ui.IconButton(Ui.Icons.Copy, $"{userCaption} kopieren", (_, _) => CopyPlain(e.Username, $"{userCaption} kopiert"))));

        if (!string.IsNullOrEmpty(e.Password))
        {
            var pw = Value(Mask(e.Password));
            pw.FontFamily = "Menlo";
            Border? toggle = null;
            toggle = Ui.IconButton(Ui.Icons.Eye, $"{pwCaption} anzeigen", (_, _) =>
            {
                _showPassword = !_showPassword;
                pw.Text = _showPassword ? e.Password : Mask(e.Password);
                Ui.SetIcon(toggle!, _showPassword ? Ui.Icons.EyeOff : Ui.Icons.Eye);
            });
            views.Add(Ui.Field(pwCaption, pw, toggle,
                Ui.IconButton(Ui.Icons.Copy, $"{pwCaption} kopieren", (_, _) => Copy(e.Password, $"{pwCaption} kopiert – wird in {MobileSettings.ClipboardSeconds} s gelöscht"))));

            if (!card)
            {
                var (meter, update) = Ui.StrengthMeter();
                update(e.Password);
                views.Add(meter);
            }
        }

        if (card && !string.IsNullOrEmpty(e.Expiry))
            views.Add(Ui.Field("Gültig bis", Value(e.Expiry),
                Ui.IconButton(Ui.Icons.Copy, "Ablaufdatum kopieren", (_, _) => Copy(e.Expiry, "Ablaufdatum kopiert"))));

        if (card && !string.IsNullOrEmpty(e.Cvc))
        {
            var cvc = Value("•••");
            cvc.FontFamily = "Menlo";
            Border? toggle = null;
            toggle = Ui.IconButton(Ui.Icons.Eye, "CVC anzeigen", (_, _) =>
            {
                var shown = cvc.Text != e.Cvc;
                cvc.Text = shown ? e.Cvc : "•••";
                Ui.SetIcon(toggle!, shown ? Ui.Icons.EyeOff : Ui.Icons.Eye);
            });
            views.Add(Ui.Field("CVC", cvc, toggle,
                Ui.IconButton(Ui.Icons.Copy, "CVC kopieren", (_, _) => Copy(e.Cvc, $"CVC kopiert – wird in {MobileSettings.ClipboardSeconds} s gelöscht"))));
        }

        if (!string.IsNullOrEmpty(e.Url))
            views.Add(Ui.Field("Website", Value(e.Url),
                Ui.IconButton(Ui.Icons.External, "Website öffnen", async (_, _) => await OpenUrl(e.Url)),
                Ui.IconButton(Ui.Icons.Copy, "Website kopieren", (_, _) => CopyPlain(e.Url, "Adresse kopiert"))));

        if (!string.IsNullOrEmpty(e.Notes))
            views.Add(Ui.Field("Notizen", Value(e.Notes, wrap: true)));

        views.Add(new Label
        {
            Text = $"Geändert {Ui.RelativeTime(e.ModifiedAt)} · erstellt {e.CreatedAt.ToLocalTime():dd.MM.yyyy}",
            TextColor = Ui.Muted,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8)
        });

        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 8, 16, 30) };
        foreach (var v in views) stack.Add(v);
        Content = Ui.PageRoot(new ScrollView { Content = stack });
        Ui.AnimateIn(views.ToArray());
    }

    private static Label Value(string text, bool wrap = false) => new()
    {
        Text = text,
        FontSize = 17,
        LineBreakMode = wrap ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation,
        Padding = new Thickness(0, 2, 0, 4)
    };

    private static string Mask(string pw) => new('•', Math.Min(pw.Length, 16));

    private void Copy(string text, string message)
    {
        SecureClipboard.Copy(text);
        Ui.Toast(this, message);
    }

    private void CopyPlain(string text, string message)
    {
        SecureClipboard.CopyPlain(text);
        Ui.Toast(this, message);
    }

    private async Task ToggleFavoriteAsync(Button button)
    {
        if (_entry == null) return;
        _entry.IsFavorite = !_entry.IsFavorite;
        App.Store.Update(_entry);
        _entry = App.Store.Get(_id);
        button.Text = _entry?.IsFavorite == true ? "Aus Favoriten entfernen" : "Zu Favoriten";
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        Ui.Toast(this, _entry?.IsFavorite == true ? "Zu Favoriten hinzugefügt" : "Aus Favoriten entfernt");
        await button.ScaleToAsync(1.04, Ui.Fast, Ui.Ease);
        await button.ScaleToAsync(1, Ui.Normal, Ui.Ease);
    }

    private async Task OpenUrl(string url)
    {
        if (!url.Contains("://")) url = "https://" + url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            Ui.Toast(this, "Keine gültige Adresse", error: true);
            return;
        }
        await Launcher.Default.OpenAsync(uri);
    }
}
