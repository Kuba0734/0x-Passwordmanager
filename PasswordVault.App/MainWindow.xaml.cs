using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Services;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace PasswordVault.App;

public sealed partial class MainWindow : Window
{
    private readonly InactivityService _inactivity;
    private DispatcherQueueTimer? _toastTimer;
    private ContentDialog? _openDialog;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1240, 800));
        AppWindow.Title = "Password Vault";
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 760;
            presenter.PreferredMinimumHeight = 560;
        }
        CenterOnScreen();

        // Theme
        ApplyTheme(App.Settings.Current.Theme);
        RootGrid.ActualThemeChanged += (_, _) => UpdateCaptionButtons();

        // Auto-Lock: jede Eingabe im Fenster setzt den Timer zurück
        _inactivity = new InactivityService(DispatcherQueue);
        _inactivity.OnTimeout += () =>
        {
            if (App.Store.IsUnlocked)
            {
                App.Store.Lock();
                ShowToast("Tresor wurde wegen Inaktivität gesperrt", "");
            }
        };
        RootGrid.AddHandler(UIElement.PointerMovedEvent,
            new PointerEventHandler((_, _) => _inactivity.ResetTimer()), true);
        RootGrid.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler((_, _) => _inactivity.ResetTimer()), true);
        RootGrid.AddHandler(UIElement.PointerWheelChangedEvent,
            new PointerEventHandler((_, _) => _inactivity.ResetTimer()), true);
        RootGrid.AddHandler(UIElement.KeyDownEvent,
            new KeyEventHandler((_, _) => _inactivity.ResetTimer()), true);

        // Tresor-Zustand kann auch aus der Browser-Erweiterung kommen → UI-Thread
        App.Store.Locked += (_, _) => DispatcherQueue.TryEnqueue(NavigateToUnlock);
        // Entsperren in der App navigiert die UnlockPage selbst (mit Animation)
        App.Store.Unlocked += (_, source) =>
        {
            if (source != VaultChangeSource.Browser) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                ShowToast("Über die Browser-Erweiterung entsperrt", "");
                NavigateToMain();
            });
        };

        App.Bridge.PairingRequested = RequestPairingAsync;
        App.Bridge.StatusChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateBridgeChip);
        UpdateBridgeChip();

        SetupTray();
        SetStateChip(false);
        RootFrame.Navigate(typeof(Views.UnlockPage));
    }

    // ================= Infobereich (Tray) =================

    private TrayIcon? _tray;
    private AutostartState _autostart = AutostartState.Disabled;

    private void SetupTray()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var icon = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        _tray = new TrayIcon(hwnd, icon, "Password Vault – gesperrt");
        _tray.Activated += () => DispatcherQueue.TryEnqueue(ShowFromTray);
        _tray.Minimized += () =>
        {
            if (App.Settings.Current.MinimizeToTray)
                DispatcherQueue.TryEnqueue(HideToTray);
        };
        _tray.MenuRequested += BuildTrayMenu;
        _tray.Show();

        Closed += (_, _) =>
        {
            _tray?.Dispose();
            _tray = null;
        };

        _ = RefreshAutostartAsync();
    }

    private System.Collections.Generic.IReadOnlyList<TrayIcon.MenuItem> BuildTrayMenu()
    {
        var unlocked = App.Store.IsUnlocked;
        return new[]
        {
            new TrayIcon.MenuItem("Password Vault öffnen", ShowFromTray),
            unlocked
                ? new TrayIcon.MenuItem("Tresor sperren", () => App.Store.Lock())
                : new TrayIcon.MenuItem("Tresor ist gesperrt", () => { }, Enabled: false),
            TrayIcon.MenuItem.Separator,
            new TrayIcon.MenuItem("Mit Windows starten", () => _ = ToggleAutostartAsync(),
                Checked: _autostart == AutostartState.Enabled,
                Enabled: _autostart is AutostartState.Enabled or AutostartState.Disabled),
            new TrayIcon.MenuItem("Beim Minimieren in den Infobereich", () =>
            {
                App.Settings.Current.MinimizeToTray = !App.Settings.Current.MinimizeToTray;
                App.Settings.Save();
            }, Checked: App.Settings.Current.MinimizeToTray),
            TrayIcon.MenuItem.Separator,
            new TrayIcon.MenuItem("Beenden", ExitApp)
        };
    }

    public async Task RefreshAutostartAsync()
        => _autostart = await AutostartService.GetStateAsync();

    public AutostartState AutostartState => _autostart;

    public async Task<AutostartState> SetAutostartAsync(bool enable)
    {
        _autostart = await AutostartService.SetAsync(enable);
        AutostartChanged?.Invoke();
        return _autostart;
    }

    /// <summary>Damit die Einstellungsseite Änderungen aus dem Tray-Menü mitbekommt.</summary>
    public event Action? AutostartChanged;

    private async Task ToggleAutostartAsync()
    {
        var state = await SetAutostartAsync(_autostart != AutostartState.Enabled);
        _tray?.ShowBalloon("Password Vault",
            state == AutostartState.Enabled ? "Startet ab jetzt automatisch mit Windows." : AutostartService.Describe(state));
    }

    /// <summary>Start ohne sichtbares Fenster (Autostart).</summary>
    public void StartInTray()
    {
        AppWindow.Hide();
    }

    public void HideToTray()
    {
        AppWindow.Hide();
        if (!App.Settings.Current.TrayHintShown)
        {
            _tray?.ShowBalloon("Password Vault läuft weiter",
                "Die App ist im Infobereich. Ein Klick auf das Symbol öffnet sie wieder.");
            App.Settings.Current.TrayHintShown = true;
            App.Settings.Save();
        }
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p)
            p.Restore();
        Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    private void ExitApp()
    {
        App.Store.Lock();
        _tray?.Dispose();
        _tray = null;
        App.Bridge.Stop();
        Application.Current.Exit();
    }

    // ================= Navigation =================

    public void NavigateToMain()
    {
        if (RootFrame.Content is Views.MainPage) return;
        RootFrame.Navigate(typeof(Views.MainPage), null,
            new DrillInNavigationTransitionInfo());
        RootFrame.BackStack.Clear();
        _inactivity.Start(App.Settings.Current.AutoLockSeconds);
        SetStateChip(true);
    }

    public void NavigateToUnlock()
    {
        _inactivity.Stop();
        _openDialog?.Hide();
        SetStateChip(false);
        if (RootFrame.Content is Views.UnlockPage) return;
        RootFrame.Navigate(typeof(Views.UnlockPage), null,
            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
        RootFrame.BackStack.Clear();
    }

    public void ApplyAutoLockSettings()
    {
        if (App.Store.IsUnlocked)
            _inactivity.Start(App.Settings.Current.AutoLockSeconds);
    }

    // ================= Dialoge =================

    /// <summary>
    /// Zeigt einen ContentDialog sicher an (WinUI erlaubt nur einen gleichzeitig).
    /// </summary>
    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_openDialog != null) return ContentDialogResult.None;
        dialog.XamlRoot = RootGrid.XamlRoot;
        dialog.RequestedTheme = RootGrid.ActualTheme;
        dialog.Style ??= (Style)Application.Current.Resources["DefaultContentDialogStyle"];
        _openDialog = dialog;
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _openDialog = null;
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool danger = false)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = confirmText,
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };
        if (danger)
            dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["DangerButtonStyle"];
        return await ShowDialogAsync(dialog) == ContentDialogResult.Primary;
    }

    public Task ShowMessageAsync(string title, string message)
        => ShowDialogAsync(new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close
        });

    /// <summary>
    /// Kopplungsanfrage der Browser-Erweiterung. Läuft auf einem Hintergrund-Thread an.
    /// </summary>
    private Task<bool> RequestPairingAsync(string code, string browser)
    {
        var tcs = new TaskCompletionSource<bool>();
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                BringToFront();
                var formatted = code.Length == 6 ? $"{code[..3]} {code[3..]}" : code;

                var panel = new StackPanel { Spacing = 14, MaxWidth = 380 };
                var icon = new Border
                {
                    Width = 56, Height = 56, CornerRadius = new CornerRadius(18),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = (Brush)Application.Current.Resources["AccentBrush"],
                    Child = new FontIcon { Glyph = "", FontSize = 24, Foreground = new SolidColorBrush(Colors.White) }
                };
                panel.Children.Add(icon);
                panel.Children.Add(new TextBlock
                {
                    Text = $"{browser} möchte sich mit deinem Tresor verbinden. Erlaube das nur, wenn du die Kopplung gerade selbst gestartet hast.",
                    TextWrapping = TextWrapping.Wrap
                });
                panel.Children.Add(new TextBlock
                {
                    Text = "Stimmt dieser Code mit dem in der Erweiterung überein?",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
                var codeBlock = new TextBlock
                {
                    Text = formatted,
                    FontSize = 40,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    CharacterSpacing = 200,
                    FontFamily = (FontFamily)Application.Current.Resources["MonoFont"],
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                panel.Children.Add(codeBlock);

                var dialog = new ContentDialog
                {
                    Title = "Browser koppeln",
                    Content = panel,
                    PrimaryButtonText = "Erlauben",
                    CloseButtonText = "Ablehnen",
                    DefaultButton = ContentDialogButton.Close
                };
                dialog.Opened += (_, _) =>
                {
                    Motion.PopIn(icon, 80);
                    Motion.FadeInUp(codeBlock, 200);
                };

                // Nach 90 Sekunden automatisch ablehnen
                var timeout = DispatcherQueue.CreateTimer();
                timeout.Interval = TimeSpan.FromSeconds(90);
                timeout.IsRepeating = false;
                timeout.Tick += (_, _) => dialog.Hide();
                timeout.Start();

                var result = await ShowDialogAsync(dialog);
                timeout.Stop();
                ReleaseFromFront();
                var approved = result == ContentDialogResult.Primary;
                if (approved) ShowToast($"{browser} wurde gekoppelt", "");
                tcs.TrySetResult(approved);
            }
            catch
            {
                ReleaseFromFront();
                tcs.TrySetResult(false);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Holt das Fenster sichtbar nach vorne, auch wenn gerade der Browser den Fokus hat.
    /// Windows verbietet Hintergrund-Apps, sich den Fokus zu nehmen – daher vorübergehend
    /// "immer im Vordergrund" plus Blinken in der Taskleiste.
    /// </summary>
    private void BringToFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            if (p.State == OverlappedPresenterState.Minimized) p.Restore();
            p.IsAlwaysOnTop = true;
        }
        AppWindow.Show();
        Activate();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetForegroundWindow(hwnd);
        var flash = new FLASHWINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = 0x3 | 0xC, // FLASHW_ALL | FLASHW_TIMERNOFG: blinken bis zum Fokus
            uCount = 0,
            dwTimeout = 0
        };
        FlashWindowEx(ref flash);
    }

    private void ReleaseFromFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter p)
            p.IsAlwaysOnTop = false;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    // ================= Toast =================

    public void ShowToast(string message, string glyph = "", int countdownSeconds = 0)
    {
        ToastText.Text = message;
        ToastIcon.Glyph = glyph;
        Toast.Visibility = Visibility.Visible;

        var visual = ElementCompositionPreview.GetElementVisual(Toast);
        visual.Opacity = 1;
        // Gleitet seitlich herein; die Leiste läuft bis zum Ausblenden ab
        Motion.FadeInX(Toast, 0, 40);

        var shown = TimeSpan.FromSeconds(countdownSeconds > 0 ? Math.Min(countdownSeconds, 6) : 2.6);
        Motion.Countdown(ToastCountdown, countdownSeconds > 0 ? TimeSpan.FromSeconds(countdownSeconds) : shown);

        _toastTimer?.Stop();
        _toastTimer = DispatcherQueue.CreateTimer();
        _toastTimer.Interval = shown;
        _toastTimer.IsRepeating = false;
        _toastTimer.Tick += (_, _) => HideToast();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        var visual = ElementCompositionPreview.GetElementVisual(Toast);
        var c = visual.Compositor;
        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);

        var ease = c.CreateCubicBezierEasingFunction(new Vector2(0.22f, 1f), new Vector2(0.36f, 1f));
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 0f, ease);
        fade.Duration = TimeSpan.FromMilliseconds(Motion.Normal);
        var slide = c.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(1f, new Vector3(40, 0, 0), ease);
        slide.Duration = fade.Duration;

        ElementCompositionPreview.SetIsTranslationEnabled(Toast, true);
        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation", slide);
        batch.End();
        batch.Completed += (_, _) =>
        {
            if (_toastTimer?.IsRunning == true) return; // neuer Toast in der Zwischenzeit
            Toast.Visibility = Visibility.Collapsed;
        };
    }

    // ================= Titelleiste & Theme =================

    private void SetStateChip(bool unlocked)
    {
        var brush = (Brush)Application.Current.Resources[unlocked ? "SuccessBrush" : "DangerBrush"];
        StateDot.Fill = brush;
        StateDotRipple.Fill = brush;
        StateText.Text = unlocked ? "Entsperrt" : "Gesperrt";
        _tray?.SetTooltip(unlocked ? "Password Vault – entsperrt" : "Password Vault – gesperrt");
        Motion.Pulse(StateDot, 1.4f);
        if (unlocked) Motion.Ripple(StateDotRipple, 2200, 0, 2.6f);
        else ElementCompositionPreview.GetElementVisual(StateDotRipple).StopAnimation("Opacity");
    }

    private void UpdateBridgeChip()
    {
        BridgeChip.Visibility = App.Bridge.IsRunning ? Visibility.Visible : Visibility.Collapsed;
        BridgeText.Text = App.Bridge.IsPaired ? "Browser gekoppelt" : "Browser bereit";
    }

    // Das Design ist immer dunkel; die frühere Theme-Einstellung wird ignoriert
    public void ApplyTheme(string theme)
    {
        RootGrid.RequestedTheme = ElementTheme.Dark;
        UpdateCaptionButtons();
    }

    private void UpdateCaptionButtons()
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        var tb = AppWindow.TitleBar;
        tb.ButtonBackgroundColor = Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Colors.Transparent;
        tb.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        tb.ButtonInactiveForegroundColor = dark
            ? Windows.UI.Color.FromArgb(120, 255, 255, 255)
            : Windows.UI.Color.FromArgb(120, 0, 0, 0);
        tb.ButtonHoverBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(25, 255, 255, 255)
            : Windows.UI.Color.FromArgb(20, 0, 0, 0);
        tb.ButtonHoverForegroundColor = tb.ButtonForegroundColor;
        tb.ButtonPressedBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(40, 255, 255, 255)
            : Windows.UI.Color.FromArgb(35, 0, 0, 0);
    }

    private void CenterOnScreen()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var size = AppWindow.Size;
        AppWindow.Move(new Windows.Graphics.PointInt32(
            area.X + (area.Width - size.Width) / 2,
            area.Y + (area.Height - size.Height) / 2));
    }
}
