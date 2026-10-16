using System;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;
using WinCredVault = Windows.Security.Credentials.PasswordVault;
using WinCredential = Windows.Security.Credentials.PasswordCredential;

namespace PasswordVault.App.Helpers;

public static class WindowsHelloService
{
    private const string CredentialResource = "PasswordVault_MasterKey";
    private const string CredentialUser = "MasterPassword";

    /// <summary>
    /// Prüft, ob Windows Hello (PIN/Biometrie) verfügbar ist.
    /// </summary>
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var result = await UserConsentVerifier.CheckAvailabilityAsync();
            return result == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Fordert den Benutzer zur Windows Hello-Authentifizierung auf.
    /// </summary>
    public static async Task<bool> AuthenticateAsync()
    {
        try
        {
            var result = await UserConsentVerifier.RequestVerificationAsync(
                "Identität bestätigen, um den Tresor zu entsperren");
            return result == UserConsentVerificationResult.Verified;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Speichert das Master-Passwort im Windows Credential Locker.
    /// </summary>
    public static void StorePassword(string masterPassword)
    {
        var vault = new WinCredVault();

        // Altes Credential entfernen falls vorhanden
        RemoveStoredPassword();

        vault.Add(new WinCredential(
            CredentialResource, CredentialUser, masterPassword));
    }

    /// <summary>
    /// Ruft das gespeicherte Master-Passwort ab.
    /// </summary>
    public static string? RetrievePassword()
    {
        try
        {
            var vault = new WinCredVault();
            var credential = vault.Retrieve(CredentialResource, CredentialUser);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Entfernt das gespeicherte Master-Passwort.
    /// </summary>
    public static void RemoveStoredPassword()
    {
        try
        {
            var vault = new WinCredVault();
            var credential = vault.Retrieve(CredentialResource, CredentialUser);
            vault.Remove(credential);
        }
        catch
        {
            // Kein Credential vorhanden — ignorieren
        }
    }

    /// <summary>
    /// Prüft, ob ein Master-Passwort gespeichert ist.
    /// </summary>
    public static bool HasStoredPassword()
    {
        try
        {
            var vault = new WinCredVault();
            var credentials = vault.FindAllByResource(CredentialResource);
            return credentials.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
