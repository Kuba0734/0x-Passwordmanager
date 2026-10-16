using Microsoft.UI.Dispatching;
using System;

namespace PasswordVault.App.Helpers;

public class InactivityService
{
    private readonly DispatcherQueueTimer _timer;
    private readonly int _timeoutSeconds;

    public event Action? OnTimeout;

    public InactivityService(
        DispatcherQueue dispatcher,
        int timeoutSeconds = 300) // 5 Minuten
    {
        _timeoutSeconds = timeoutSeconds;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(
            _timeoutSeconds);
        _timer.IsRepeating = false;
        _timer.Tick += (s, e) => OnTimeout?.Invoke();
    }

    /// <summary>
    /// Setzt den Timer zurück (bei jeder Benutzeraktion
    /// aufrufen).
    /// </summary>
    public void ResetTimer()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }
}
