using System;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Dispatching;

namespace PasswordVault.App.Helpers;

public static class ClipboardHelper
{
    private static DispatcherQueueTimer? _clearTimer;

    /// <summary>
    /// Kopiert Text und löscht die Zwischenablage
    /// nach 15 Sekunden automatisch.
    /// </summary>
    public static void CopyWithAutoClear(
        string text,
        DispatcherQueue dispatcher,
        int clearAfterSeconds = 15)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);

        // Alten Timer stoppen
        _clearTimer?.Stop();

        // Neuen Timer starten
        _clearTimer = dispatcher.CreateTimer();
        _clearTimer.Interval = TimeSpan.FromSeconds(
            clearAfterSeconds);
        _clearTimer.IsRepeating = false;
        _clearTimer.Tick += (s, e) =>
        {
            Clipboard.SetContent(new DataPackage());
            _clearTimer = null;
        };
        _clearTimer.Start();
    }
}
