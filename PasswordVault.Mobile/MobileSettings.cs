namespace PasswordVault.Mobile;

/// <summary>
/// Einstellungen dieses iPhones (nicht Teil des Tresors, werden also nicht mit dem PC abgeglichen).
/// </summary>
public static class MobileSettings
{
    private static IPreferences P => Preferences.Default;

    /// <summary>Sperren, wenn die App länger im Hintergrund war; 0 = sofort.</summary>
    public static int LockAfterSeconds
    {
        get => P.Get(nameof(LockAfterSeconds), 60);
        set => P.Set(nameof(LockAfterSeconds), value);
    }

    public static int ClipboardSeconds
    {
        get => P.Get(nameof(ClipboardSeconds), 30);
        set => P.Set(nameof(ClipboardSeconds), value);
    }

    public static bool BiometricEnabled
    {
        get => P.Get(nameof(BiometricEnabled), false);
        set => P.Set(nameof(BiometricEnabled), value);
    }

    // ===== Generator-Standard, wie am PC =====
    public static int Length
    {
        get => P.Get(nameof(Length), 20);
        set => P.Set(nameof(Length), value);
    }

    public static bool Upper
    {
        get => P.Get(nameof(Upper), true);
        set => P.Set(nameof(Upper), value);
    }

    public static bool Lower
    {
        get => P.Get(nameof(Lower), true);
        set => P.Set(nameof(Lower), value);
    }

    public static bool Digits
    {
        get => P.Get(nameof(Digits), true);
        set => P.Set(nameof(Digits), value);
    }

    public static bool Symbols
    {
        get => P.Get(nameof(Symbols), true);
        set => P.Set(nameof(Symbols), value);
    }

    public static bool ExcludeAmbiguous
    {
        get => P.Get(nameof(ExcludeAmbiguous), false);
        set => P.Set(nameof(ExcludeAmbiguous), value);
    }

    public static readonly (string Label, int Seconds)[] LockChoices =
    {
        ("Sofort", 0), ("Nach 30 Sekunden", 30), ("Nach 1 Minute", 60), ("Nach 5 Minuten", 300), ("Nach 15 Minuten", 900)
    };

    public static readonly (string Label, int Seconds)[] ClipboardChoices =
    {
        ("Nach 10 Sekunden", 10), ("Nach 30 Sekunden", 30), ("Nach 60 Sekunden", 60), ("Nach 2 Minuten", 120)
    };
}
