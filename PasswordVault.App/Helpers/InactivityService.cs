using Microsoft.UI.Dispatching;
using System;

namespace PasswordVault.App.Helpers;

public class InactivityService
{
    private readonly DispatcherQueueTimer _timer;
    private bool _active;

    public event Action? OnTimeout;

    public InactivityService(DispatcherQueue dispatcher)
    {
        _timer = dispatcher.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += (s, e) =>
        {
            _active = false;
            OnTimeout?.Invoke();
        };
    }

    /// <summary>
    /// Startet die Überwachung. 0 Sekunden = Auto-Lock deaktiviert.
    /// </summary>
    public void Start(int timeoutSeconds)
    {
        _timer.Stop();
        _active = timeoutSeconds > 0;
        if (!_active) return;
        _timer.Interval = TimeSpan.FromSeconds(timeoutSeconds);
        _timer.Start();
    }

    /// <summary>
    /// Setzt den Timer zurück (bei jeder Benutzeraktion aufrufen).
    /// </summary>
    public void ResetTimer()
    {
        if (!_active) return;
        _timer.Stop();
        _timer.Start();
    }

    public void Stop()
    {
        _active = false;
        _timer.Stop();
    }
}
