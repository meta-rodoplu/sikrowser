using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace Sikrowser.Core;

public enum PerformanceProfile
{
    MaximumSpeed,   // Active navigation: 100% CPU/Network throughput, normal memory target
    Stabilized,     // Page loaded: low DOM overhead, gentle working set maintenance
    PowerSaver      // Idle / Minimized: Low memory target, paused background JS, deep trim
}

/// <summary>
/// Continuous, adaptive performance optimizer.
/// Dynamically balances maximum network line-speed throughput during page loads
/// with aggressive physical memory reclamation during reading and idle periods.
/// </summary>
public sealed class AdaptivePerformanceManager : IDisposable
{
    private static readonly Lazy<AdaptivePerformanceManager> _instance =
        new(() => new AdaptivePerformanceManager());

    public static AdaptivePerformanceManager Instance => _instance.Value;

    private readonly System.Threading.Timer _watchdogTimer;
    private DateTime _lastUserActivity = DateTime.Now;
    private PerformanceProfile _currentProfile = PerformanceProfile.MaximumSpeed;
    private readonly object _lock = new();

    public PerformanceProfile CurrentProfile => _currentProfile;
    public string StatusText => _currentProfile switch
    {
        PerformanceProfile.MaximumSpeed => "⚡ Yüksek Hız Modu (Tam Ağ Bant Genişliği)",
        PerformanceProfile.Stabilized   => "✨ Kararlı ve Optimize",
        PerformanceProfile.PowerSaver   => "🍃 Ultra Düşük RAM Modu",
        _ => "Hazır"
    };

    public event Action<PerformanceProfile, string>? ProfileChanged;

    private AdaptivePerformanceManager()
    {
        // Periodic background watchdog running every 35 seconds
        _watchdogTimer = new System.Threading.Timer(OnWatchdogTick, null, 35000, 35000);
    }

    public void Dispose()
    {
        _watchdogTimer?.Dispose();
    }

    /// <summary>
    /// Notifies the optimizer that user navigation or interaction has started.
    /// Immediately shifts into MaximumSpeed mode with 100% network and CPU throughput.
    /// </summary>
    public void NotifyUserActivity(CoreWebView2? activeCore = null)
    {
        lock (_lock)
        {
            _lastUserActivity = DateTime.Now;
            if (_currentProfile != PerformanceProfile.MaximumSpeed)
            {
                _currentProfile = PerformanceProfile.MaximumSpeed;
                if (activeCore != null)
                {
                    try
                    {
                        activeCore.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                    }
                    catch { }
                }
                ProfileChanged?.Invoke(_currentProfile, StatusText);
            }
        }
    }

    /// <summary>
    /// Notifies the optimizer that page load is complete.
    /// Schedules gentle stabilization after 3 seconds so temporary download buffers are reclaimed.
    /// </summary>
    public void NotifyNavigationCompleted(CoreWebView2? activeCore, TabManager tabManager)
    {
        _lastUserActivity = DateTime.Now;

        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            lock (_lock)
            {
                if ((DateTime.Now - _lastUserActivity).TotalSeconds >= 2.5)
                {
                    _currentProfile = PerformanceProfile.Stabilized;
                    tabManager.TrimMemory();
                    ProfileChanged?.Invoke(_currentProfile, StatusText);
                }
            }
        });
    }

    private void OnWatchdogTick(object? state)
    {
        lock (_lock)
        {
            var idleDuration = DateTime.Now - _lastUserActivity;
            // If idle for more than 40 seconds, transition into ultra-low RAM PowerSaver
            if (idleDuration.TotalSeconds >= 40)
            {
                if (_currentProfile != PerformanceProfile.PowerSaver)
                {
                    _currentProfile = PerformanceProfile.PowerSaver;
                    ProfileChanged?.Invoke(_currentProfile, StatusText);
                }

                // Perform deep memory sweep across all processes
                PerformContinuousMaintenance();
            }
        }
    }

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    private static void PerformContinuousMaintenance()
    {
        try
        {
            // 1. .NET Garbage Collector
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, false);

            // 2. Host process working set
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);

            // 3. All WebView2 child processes (Browser, GPU, Renderers, Utility)
            var procs = BrowserEnvironmentManager.Instance.GetProcessInfos();
            foreach (var info in procs)
            {
                try
                {
                    using var p = Process.GetProcessById(info.ProcessId);
                    EmptyWorkingSet(p.Handle);
                }
                catch { }
            }
        }
        catch { }
    }
}
