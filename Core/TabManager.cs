using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Sikrowser.Core;

public class BrowserTab
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; set; } = "Yeni Sekme";
    public string Url { get; set; } = "about:blank";
    public bool IsHibernated { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsLoading { get; set; }
    public DateTime LastActiveTimeUtc { get; set; } = DateTime.UtcNow;
    public WebView2? WebViewControl { get; set; }

    public event Action<BrowserTab>? StateChanged;

    public void NotifyStateChanged()
    {
        StateChanged?.Invoke(this);
    }

    /// <summary>
    /// Freezes (suspends) the tab to stop background timers, JS execution,
    /// and rendering while keeping DOM state in memory for zero-latency wake-up.
    /// </summary>
    public async Task SuspendAsync()
    {
        if (WebViewControl?.CoreWebView2 == null || IsSuspended || IsHibernated) return;

        try
        {
            await WebViewControl.CoreWebView2.TrySuspendAsync();
            IsSuspended = true;
            NotifyStateChanged();
        }
        catch { }
    }

    /// <summary>
    /// Resumes a suspended tab back to active execution.
    /// </summary>
    public void Resume()
    {
        if (WebViewControl?.CoreWebView2 == null || !IsSuspended) return;

        try
        {
            WebViewControl.CoreWebView2.Resume();
            IsSuspended = false;
            NotifyStateChanged();
        }
        catch { }
    }

    /// <summary>
    /// Hibernates the tab by cleanly disposing its WebView2 control and freeing
    /// its renderer process from RAM, while keeping URL and metadata intact.
    /// </summary>
    public void Hibernate()
    {
        if (WebViewControl == null || IsHibernated)
        {
            return;
        }

        try
        {
            if (WebViewControl.CoreWebView2 != null)
            {
                Url = WebViewControl.CoreWebView2.Source ?? Url;
                Title = WebViewControl.CoreWebView2.DocumentTitle ?? Title;
            }

            if (WebViewControl.Parent != null)
            {
                WebViewControl.Parent.Controls.Remove(WebViewControl);
            }

            WebViewControl.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }
        finally
        {
            WebViewControl = null;
            IsSuspended = false;
            IsHibernated = true;
            NotifyStateChanged();
        }
    }
}

public sealed class TabManager : IDisposable
{
    private readonly Control _container;
    private readonly List<BrowserTab> _tabs = new();
    private BrowserTab? _activeTab;
    private readonly System.Threading.Timer _maintenanceTimer;
    private readonly HashSet<Guid> _dismissedHighResourceWarnings = new();
    private bool _isDisposed;

    public IReadOnlyList<BrowserTab> Tabs => _tabs.AsReadOnly();
    public BrowserTab? ActiveTab => _activeTab;

    public event Action<BrowserTab?>? ActiveTabChanged;
    public event Action? TabsListChanged;
    public event Action<string>? StatusMessage;
    public event Action<BrowserTab, double>? HighResourceTabDetected;

    public TabManager(Control container)
    {
        _container = container;
        // Periodic background tab lifecycle maintenance every 10 seconds
        _maintenanceTimer = new System.Threading.Timer(OnMaintenanceTick, null, 10000, 10000);
    }

    private void OnMaintenanceTick(object? state)
    {
        if (_isDisposed) return;

        try
        {
            PerformTabLifecycleMaintenance();
            CheckForHighResourceUsage();
        }
        catch { }
    }

    /// <summary>
    /// Enforces strict tab memory policies:
    /// 1. Maximum 3 tabs kept in live RAM at once.
    /// 2. Background tabs idle > 1 min are frozen (suspended).
    /// 3. Background tabs idle > 5 mins (or 4th, 5th+ excess tabs) are hibernated (evicted from RAM).
    /// </summary>
    public void PerformTabLifecycleMaintenance()
    {
        if (_isDisposed) return;
        DateTime now = DateTime.UtcNow;

        List<BrowserTab> liveTabs;
        lock (_tabs)
        {
            liveTabs = _tabs.Where(t => t.WebViewControl != null && !t.IsHibernated).ToList();
        }

        // Active tab is always preserved
        var inactiveLiveTabs = liveTabs
            .Where(t => t != _activeTab)
            .OrderByDescending(t => t.LastActiveTimeUtc)
            .ToList();

        int liveCount = (_activeTab?.WebViewControl != null ? 1 : 0);

        for (int i = 0; i < inactiveLiveTabs.Count; i++)
        {
            var tab = inactiveLiveTabs[i];
            var idleTime = now - tab.LastActiveTimeUtc;

            // Rule 1: Idle for > 5 minutes -> completely hibernate (free from RAM)
            if (idleTime >= BrowserConfig.TabHibernateTimeout)
            {
                tab.Hibernate();
                continue;
            }

            // Rule 2: If we already have 3 live tabs in RAM (i.e. this is 4th, 5th, etc. tab)
            // or if it has been idle for > 1 minute -> freeze/suspend!
            if (liveCount >= BrowserConfig.MaxActiveTabsInRam)
            {
                if (!tab.IsSuspended)
                {
                    _ = tab.SuspendAsync();
                }
            }
            else
            {
                if (idleTime >= BrowserConfig.TabFreezeTimeout)
                {
                    if (!tab.IsSuspended)
                    {
                        _ = tab.SuspendAsync();
                    }
                }
                liveCount++;
            }
        }
    }

    private void CheckForHighResourceUsage()
    {
        if (_isDisposed) return;

        try
        {
            var snap = ProcessMemoryTracker.CaptureSnapshot();
            foreach (var proc in snap.WebViewProcesses)
            {
                // If any Renderer process exceeds 350 MB RAM
                if (proc.Kind.Equals("Renderer", StringComparison.OrdinalIgnoreCase) && proc.WorkingSetMB > 350.0)
                {
                    var tabToWarn = _activeTab;
                    if (tabToWarn != null)
                    {
                        lock (_dismissedHighResourceWarnings)
                        {
                            if (_dismissedHighResourceWarnings.Contains(tabToWarn.Id)) continue;
                        }
                        HighResourceTabDetected?.Invoke(tabToWarn, proc.WorkingSetMB);
                        break;
                    }
                }
            }
        }
        catch { }
    }

    public void DismissHighResourceWarning(Guid tabId)
    {
        lock (_dismissedHighResourceWarnings)
        {
            _dismissedHighResourceWarnings.Add(tabId);
        }
    }

    /// <summary>
    /// Creates a new tab. If activate is true, activates it and hibernates inactive tabs
    /// to maintain ultra-low RAM usage.
    /// </summary>
    public async Task<BrowserTab> CreateTabAsync(string url = "about:blank", bool activate = true)
    {
        var tab = new BrowserTab
        {
            Url = string.IsNullOrWhiteSpace(url) ? "about:blank" : url,
            Title = "Yeni Sekme",
            LastActiveTimeUtc = DateTime.UtcNow
        };

        _tabs.Add(tab);
        TabsListChanged?.Invoke();

        if (activate || _activeTab == null)
        {
            await SwitchToTabAsync(tab);
        }
        else
        {
            // Created in hibernated state directly
            tab.IsHibernated = true;
        }

        return tab;
    }

    /// <summary>
    /// Switches to the specified tab. Wakes up or resumes if needed,
    /// enforcing the maximum 3 live tabs in RAM limit.
    /// </summary>
    public async Task SwitchToTabAsync(BrowserTab targetTab)
    {
        if (!_tabs.Contains(targetTab))
        {
            return;
        }

        if (_activeTab == targetTab && targetTab.WebViewControl != null)
        {
            targetTab.LastActiveTimeUtc = DateTime.UtcNow;
            if (targetTab.IsSuspended)
            {
                targetTab.Resume();
            }
            return;
        }

        if (_activeTab != null)
        {
            _activeTab.LastActiveTimeUtc = DateTime.UtcNow;
        }

        _activeTab = targetTab;
        targetTab.LastActiveTimeUtc = DateTime.UtcNow;

        // Wake up target tab if hibernated or not yet created
        if (targetTab.WebViewControl == null || targetTab.IsHibernated)
        {
            await InitializeWebViewForTabAsync(targetTab);
        }
        else
        {
            if (!_container.Controls.Contains(targetTab.WebViewControl))
            {
                _container.Controls.Clear();
                _container.Controls.Add(targetTab.WebViewControl);
            }
            targetTab.WebViewControl.BringToFront();
            targetTab.WebViewControl.Focus();

            if (targetTab.IsSuspended)
            {
                targetTab.Resume();
            }
        }

        targetTab.IsHibernated = false;
        targetTab.IsSuspended = false;

        // Enforce maximum 3 live tabs in RAM limit
        EnforceMaxTabsLimit();

        ActiveTabChanged?.Invoke(targetTab);
        TabsListChanged?.Invoke();
    }

    private void EnforceMaxTabsLimit()
    {
        var liveInactive = _tabs
            .Where(t => t != _activeTab && t.WebViewControl != null && !t.IsHibernated)
            .OrderBy(t => t.LastActiveTimeUtc) // oldest first
            .ToList();

        // Active tab counts as 1. If liveInactive.Count >= MaxActiveTabsInRam (3), hibernate the oldest!
        while (liveInactive.Count >= BrowserConfig.MaxActiveTabsInRam)
        {
            var oldest = liveInactive[0];
            oldest.Hibernate();
            liveInactive.RemoveAt(0);
        }
    }

    /// <summary>
    /// Closes a tab, disposing resources cleanly.
    /// </summary>
    public async Task CloseTabAsync(BrowserTab tab)
    {
        int index = _tabs.IndexOf(tab);
        if (index < 0) return;

        tab.Hibernate();
        _tabs.RemoveAt(index);

        if (_activeTab == tab)
        {
            _activeTab = null;
            if (_tabs.Count > 0)
            {
                int newIndex = Math.Min(index, _tabs.Count - 1);
                await SwitchToTabAsync(_tabs[newIndex]);
            }
            else
            {
                // Always keep at least 1 tab open
                await CreateTabAsync("about:blank", true);
            }
        }

        TabsListChanged?.Invoke();
    }

    /// <summary>
    /// Disposes all tabs and their underlying WebView2 controls immediately.
    /// Used during application shutdown to avoid hanging processes.
    /// </summary>
    public void DisposeAllTabs()
    {
        for (int i = _tabs.Count - 1; i >= 0; i--)
        {
            try
            {
                _tabs[i].Hibernate();
            }
            catch { }
        }
        _tabs.Clear();
        _container.Controls.Clear();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _maintenanceTimer?.Dispose();
        DisposeAllTabs();
    }

    private async Task InitializeWebViewForTabAsync(BrowserTab tab)
    {
        var webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        _container.Controls.Clear();
        _container.Controls.Add(webView);
        tab.WebViewControl = webView;

        StatusMessage?.Invoke("Sikrowser çekirdeği başlatılıyor...");

        // Use the SINGLE shared CoreWebView2Environment with InPrivate mode (Zero persistent history/cache)
        var env = await BrowserEnvironmentManager.Instance.GetEnvironmentAsync();
        var controllerOptions = env.CreateCoreWebView2ControllerOptions();
        controllerOptions.IsInPrivateModeEnabled = true;
        await webView.EnsureCoreWebView2Async(env, controllerOptions);

        var core = webView.CoreWebView2;

        // Optimize settings
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.IsBuiltInErrorPageEnabled = true;

        // Apply low memory target
        try
        {
            core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
        }
        catch
        {
            // Older versions fallback
        }

        // Intercept network requests for ad and tracker blocking
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;

        // Wire events
        core.NavigationStarting += (s, e) =>
        {
            tab.IsLoading = true;
            tab.Url = e.Uri;
            AdaptivePerformanceManager.Instance.NotifyUserActivity(core);
            tab.NotifyStateChanged();
        };

        core.NavigationCompleted += (s, e) =>
        {
            tab.IsLoading = false;
            tab.Url = core.Source;
            tab.Title = string.IsNullOrWhiteSpace(core.DocumentTitle) ? tab.Url : core.DocumentTitle;
            AdaptivePerformanceManager.Instance.NotifyNavigationCompleted(core, this);
            tab.NotifyStateChanged();
        };

        core.DocumentTitleChanged += (s, e) =>
        {
            tab.Title = string.IsNullOrWhiteSpace(core.DocumentTitle) ? tab.Url : core.DocumentTitle;
            tab.NotifyStateChanged();
        };

        core.SourceChanged += (s, e) =>
        {
            tab.Url = core.Source;
            tab.NotifyStateChanged();
        };

        // Navigate to the tab's target URL
        if (!string.IsNullOrWhiteSpace(tab.Url) && tab.Url != "about:blank")
        {
            core.Navigate(tab.Url);
        }
        else
        {
            core.Navigate("about:blank");
        }

        tab.NotifyStateChanged();
        StatusMessage?.Invoke("Hazır");
    }

    private static void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            if (Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
            {
                if (AdBlockEngine.Instance.ShouldBlock(uri, out _))
                {
                    var env = BrowserEnvironmentManager.Instance.Environment;
                    if (env != null)
                    {
                        // Immediate HTTP 204 No Content with empty stream saves network bandwidth and render RAM
                        e.Response = env.CreateWebResourceResponse(
                            Stream.Null,
                            204,
                            "No Content",
                            "Content-Type: text/plain"
                        );
                    }
                }
            }
        }
        catch
        {
            // Do not break web compatibility on filtering exception
        }
    }

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    /// <summary>
    /// Trims memory across both .NET CLR and Microsoft WebView2 Chromium engine
    /// and purges cold physical memory pages across all child processes.
    /// </summary>
    public void TrimMemory()
    {
        try
        {
            // Signal WebView2 to flush cache and garbage-collect
            if (_activeTab?.WebViewControl?.CoreWebView2 != null)
            {
                _activeTab.WebViewControl.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
            }

            // Flush .NET Garbage Collector
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

            // Empty Working Set for host process
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);

            // Empty Working Set for all WebView2 child processes (Browser, GPU, Renderers, Utility)
            foreach (var pInfo in BrowserEnvironmentManager.Instance.GetProcessInfos())
            {
                try
                {
                    using var p = Process.GetProcessById(pInfo.ProcessId);
                    EmptyWorkingSet(p.Handle);
                }
                catch { }
            }
        }
        catch
        {
            // Non-critical optimization
        }
    }

    /// <summary>
    /// Purges all session browsing data (history, cache, cookies, local storage) from memory.
    /// </summary>
    public async Task ClearAllBrowsingDataAsync()
    {
        try
        {
            if (_activeTab?.WebViewControl?.CoreWebView2?.Profile != null)
            {
                await _activeTab.WebViewControl.CoreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.AllProfile
                );
            }
        }
        catch { }
    }

    /// <summary>
    /// Suspends the active WebView when the application is minimized or hidden.
    /// Frees CPU cycles and pauses rendering.
    /// </summary>
    public async Task SuspendActiveTabAsync()
    {
        if (_activeTab?.WebViewControl?.CoreWebView2 != null)
        {
            try
            {
                _activeTab.WebViewControl.Visible = false;
                await _activeTab.WebViewControl.CoreWebView2.TrySuspendAsync();
            }
            catch { }
        }
    }

    /// <summary>
    /// Resumes the active WebView when restored into view.
    /// </summary>
    public void ResumeActiveTab()
    {
        if (_activeTab?.WebViewControl?.CoreWebView2 != null)
        {
            try
            {
                _activeTab.WebViewControl.Visible = true;
                _activeTab.WebViewControl.CoreWebView2.Resume();
            }
            catch { }
        }
    }
}
