using Foundation;
using LocalAuthentication;
using Security;

namespace PasswordVault.Mobile;

/// <summary>
/// Face ID / Touch ID: Das Master-Passwort liegt im Schlüsselbund und lässt sich nur nach
/// erfolgreicher Biometrie lesen. Neu angelernte Gesichter/Finger machen den Eintrag ungültig.
/// </summary>
public static class Biometrics
{
    private const string Service = "de.privat.passwordvault";
    private const string Account = "master";

    public static bool IsAvailable => new LAContext().CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);

    public static string Name
    {
        get
        {
            var context = new LAContext();
            context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);
            return context.BiometryType == LABiometryType.TouchId ? "Touch ID" : "Face ID";
        }
    }

    public static bool IsEnabled => MobileSettings.BiometricEnabled && IsAvailable;

    public static bool Store(string password)
    {
        Remove();
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = Service,
            Account = Account,
            AccessControl = new SecAccessControl(SecAccessible.WhenPasscodeSetThisDeviceOnly, SecAccessControlCreateFlags.BiometryCurrentSet),
            ValueData = NSData.FromString(password, NSStringEncoding.UTF8)
        };
        return SecKeyChain.Add(record) == SecStatusCode.Success;
    }

    /// <summary>Zeigt die Face-ID-Abfrage; blockiert, daher nicht im UI-Thread aufrufen.</summary>
    public static string? Load()
    {
        var query = new SecRecord(SecKind.GenericPassword)
        {
            Service = Service,
            Account = Account,
            UseOperationPrompt = "Tresor entsperren"
        };
        var data = SecKeyChain.QueryAsData(query, false, out var status);
        return status == SecStatusCode.Success && data != null ? new NSString(data, NSStringEncoding.UTF8).ToString() : null;
    }

    public static void Remove() => SecKeyChain.Remove(new SecRecord(SecKind.GenericPassword) { Service = Service, Account = Account });
}
