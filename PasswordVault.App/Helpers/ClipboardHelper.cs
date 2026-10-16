using System;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Dispatching;

namespace PasswordVault.App.Helpers;

public static class ClipboardHelper
{
    private static DispatcherQueueTimer? _clearTimer;
    private static string? _lastCopied;

    /// <summary>
    /// Kopiert Text, blendet einen Toast ein und leert die Zwischenablage nach
    /// der eingestellten Zeit – aber nur, wenn dort noch unser Text steht.
    /// </summary>
    public static void CopySecret(string text, string what = "Passwort")
    {
        if (string.IsNullOrEmpty(text))
        {
            App.MainWindowInstance.ShowToast($"{what} ist leer", "");
            return;
        }

        var seconds = App.Settings.Current.ClipboardClearSeconds;
        var package = new DataPackage();
        package.SetText(text);
        // Nicht im Windows-Zwischenablageverlauf / Cloud-Sync speichern
        Clipboard.SetContentWithOptions(package, new ClipboardContentOptions
        {
            IsAllowedInHistory = false,
            IsRoamable = false
        });
        _lastCopied = text;

        _clearTimer?.Stop();
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _clearTimer = dispatcher.CreateTimer();
        _clearTimer.Interval = TimeSpan.FromSeconds(seconds);
        _clearTimer.IsRepeating = false;
        _clearTimer.Tick += async (s, e) =>
        {
            _clearTimer = null;
            try
            {
                var content = Clipboard.GetContent();
                if (content.Contains(StandardDataFormats.Text)
                    && await content.GetTextAsync() == _lastCopied)
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
                // Zwischenablage gerade von anderer App blockiert – nicht kritisch
            }
            _lastCopied = null;
        };
        _clearTimer.Start();

        App.MainWindowInstance.ShowToast(
            $"{what} kopiert · wird in {seconds} s gelöscht", "", seconds);
    }

    /// <summary>Kopiert unkritischen Text (z.B. Benutzername) ohne Auto-Löschen.</summary>
    public static void CopyPlain(string text, string what)
    {
        if (string.IsNullOrEmpty(text))
        {
            App.MainWindowInstance.ShowToast($"{what} ist leer", "");
            return;
        }
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        App.MainWindowInstance.ShowToast($"{what} kopiert", "");
    }
}
