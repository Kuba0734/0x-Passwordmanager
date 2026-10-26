using Foundation;
using UIKit;

namespace PasswordVault.Mobile;

public static class SecureClipboard
{
    /// <summary>
    /// Kopiert nur auf dieses Gerät (keine Übertragung per Universal Clipboard an Mac/iPad)
    /// und lässt iOS den Inhalt nach der eingestellten Zeit selbst löschen – auch wenn die App dann gesperrt ist.
    /// </summary>
    public static void Copy(string text)
    {
        var item = new NSDictionary<NSString, NSObject>(new NSString("public.utf8-plain-text"), new NSString(text));
        UIPasteboard.General.SetItems(new[] { item }, new UIPasteboardOptions
        {
            LocalOnly = true,
            ExpirationDate = NSDate.FromTimeIntervalSinceNow(MobileSettings.ClipboardSeconds)
        });
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }

    /// <summary>Für Benutzername und Website: bleibt in der Zwischenablage.</summary>
    public static void CopyPlain(string text)
    {
        UIPasteboard.General.String = text;
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
}
