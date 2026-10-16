using Foundation;
using UIKit;

namespace PasswordVault.Mobile;

public static class SecureClipboard
{
    private const int ClearAfterSeconds = 30;

    /// <summary>
    /// Kopiert nur auf dieses Gerät (keine Übertragung per Universal Clipboard an Mac/iPad)
    /// und lässt iOS den Inhalt nach 30 Sekunden selbst löschen – auch wenn die App dann gesperrt ist.
    /// </summary>
    public static void Copy(string text)
    {
        var item = new NSDictionary<NSString, NSObject>(new NSString("public.utf8-plain-text"), new NSString(text));
        UIPasteboard.General.SetItems(new[] { item }, new UIPasteboardOptions
        {
            LocalOnly = true,
            ExpirationDate = NSDate.FromTimeIntervalSinceNow(ClearAfterSeconds)
        });
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
}
