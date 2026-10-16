using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordVault.App;

/// <summary>
/// Eigener Einstiegspunkt: Es läuft immer nur eine Instanz. Ein zweiter Start
/// (z.B. Klick im Startmenü, während die App im Infobereich liegt) holt
/// das vorhandene Fenster nach vorne, statt eine zweite App zu öffnen.
/// </summary>
public static class Program
{
    private const string InstanceKey = "PasswordVault.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            RedirectToMainInstance(mainInstance);
            return 0;
        }

        mainInstance.Activated += (_, _) => App.OnSecondInstanceActivated();

        Application.Start(callback =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    /// <summary>
    /// Leitet die Aktivierung an die laufende Instanz weiter. Der STA-Thread darf dabei
    /// nicht blockieren, daher das Warten über CoWaitForMultipleObjects (Muster aus der WinAppSDK-Doku).
    /// </summary>
    private static void RedirectToMainInstance(AppInstance mainInstance)
    {
        var args = AppInstance.GetCurrent().GetActivatedEventArgs();
        var done = CreateEvent(IntPtr.Zero, true, false, null);
        Task.Run(() =>
        {
            try { mainInstance.RedirectActivationToAsync(args).AsTask().Wait(); }
            catch { /* Hauptinstanz beendet sich gerade */ }
            SetEvent(done);
        });
        CoWaitForMultipleObjects(0 /*CWMO_DEFAULT*/, 0xFFFFFFFF, 1, new[] { done }, out _);
        CloseHandle(done);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(IntPtr attributes, bool manualReset, bool initialState, string? name);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(uint flags, uint timeout, uint count, IntPtr[] handles, out uint index);
}
