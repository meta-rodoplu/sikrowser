using System.Diagnostics;
using Sikrowser.Core;
using Microsoft.Web.WebView2.Core;

namespace Sikrowser.Benchmark;

public class BenchmarkResult
{
    public string Scenario { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool AdBlockActive { get; set; }
    public double HostWorkingSetMB { get; set; }
    public double WebViewWorkingSetMB { get; set; }
    public double TotalWorkingSetMB { get; set; }
    public double TotalPrivateMB { get; set; }
    public int ProcessCount { get; set; }
    public long BlockedRequests { get; set; }
    public TimeSpan LoadDuration { get; set; }
    public MemorySnapshot Snapshot { get; set; } = null!;
}

public sealed class BenchmarkRunner
{
    private readonly TabManager _tabManager;
    private readonly Action<string> _logger;

    public BenchmarkRunner(TabManager tabManager, Action<string>? logger = null)
    {
        _tabManager = tabManager;
        _logger = logger ?? Console.WriteLine;
    }

    /// <summary>
    /// Executes the comprehensive 5-phase RAM benchmark:
    /// Phase 1: Idle (Boşta - about:blank)
    /// Phase 2: Simple Page (Basit sayfa)
    /// Phase 3A: Heavy Page (Yoğun sayfa - AdBlock KAPALI)
    /// Phase 3B: Heavy Page (Yoğun sayfa - AdBlock AÇIK)
    /// Phase 4: Memory Trim (Kırpma sonrası)
    /// Phase 5: Multi-Tab Hibernation (5 sekme açıkken uyutma)
    /// </summary>
    public async Task<List<BenchmarkResult>> RunAllBenchmarksAsync(
        string simplePageUrl = "https://example.com",
        string heavyPageUrl = "https://edition.cnn.com")
    {
        var results = new List<BenchmarkResult>();
        _logger("==================================================");
        _logger("    SIKROWSER ULTRA-LOW RAM BENCHMARK RUNNER      ");
        _logger("==================================================");

        var activeTab = _tabManager.ActiveTab;
        if (activeTab == null)
        {
            activeTab = await _tabManager.CreateTabAsync("about:blank", true);
        }

        // -----------------------------------------------------------------
        // FAZ 1: BOŞTA (IDLE) - about:blank
        // -----------------------------------------------------------------
        _logger("\n[FAZ 1/5] Boşta (Idle) Durum Ölçülüyor...");
        await NavigateAndWaitAsync(activeTab, "about:blank", 10000);
        await Task.Delay(2500);
        _tabManager.TrimMemory();
        await Task.Delay(1000);

        var idleSnapshot = ProcessMemoryTracker.CaptureSnapshot();
        var idleResult = new BenchmarkResult
        {
            Scenario = "Boşta (Idle - about:blank)",
            Url = "about:blank",
            AdBlockActive = AdBlockEngine.Instance.IsEnabled,
            HostWorkingSetMB = idleSnapshot.HostWorkingSetMB,
            WebViewWorkingSetMB = idleSnapshot.WebViewWorkingSetMB,
            TotalWorkingSetMB = idleSnapshot.TotalWorkingSetMB,
            TotalPrivateMB = idleSnapshot.TotalPrivateMB,
            ProcessCount = 1 + idleSnapshot.WebViewProcesses.Count,
            BlockedRequests = AdBlockEngine.Instance.BlockedRequests,
            Snapshot = idleSnapshot
        };
        results.Add(idleResult);
        _logger(idleSnapshot.DetailedBreakdown());

        // -----------------------------------------------------------------
        // FAZ 2: BASİT SAYFA (SIMPLE PAGE)
        // -----------------------------------------------------------------
        _logger($"\n[FAZ 2/5] Basit Sayfa Ölçülüyor: {simplePageUrl} ...");
        var swSimple = Stopwatch.StartNew();
        await NavigateAndWaitAsync(activeTab, simplePageUrl, 15000);
        swSimple.Stop();

        await Task.Delay(3000);
        var simpleSnapshot = ProcessMemoryTracker.CaptureSnapshot();
        var simpleResult = new BenchmarkResult
        {
            Scenario = "Basit Sayfa (Lightweight Page)",
            Url = simplePageUrl,
            AdBlockActive = AdBlockEngine.Instance.IsEnabled,
            HostWorkingSetMB = simpleSnapshot.HostWorkingSetMB,
            WebViewWorkingSetMB = simpleSnapshot.WebViewWorkingSetMB,
            TotalWorkingSetMB = simpleSnapshot.TotalWorkingSetMB,
            TotalPrivateMB = simpleSnapshot.TotalPrivateMB,
            ProcessCount = 1 + simpleSnapshot.WebViewProcesses.Count,
            BlockedRequests = AdBlockEngine.Instance.BlockedRequests,
            LoadDuration = swSimple.Elapsed,
            Snapshot = simpleSnapshot
        };
        results.Add(simpleResult);
        _logger(simpleSnapshot.DetailedBreakdown());

        // -----------------------------------------------------------------
        // FAZ 3A: YOĞUN SAYFA (AdBlock KAPALI)
        // -----------------------------------------------------------------
        _logger($"\n[FAZ 3A/5] Yoğun Web Sayfası (AdBlock KAPALI): {heavyPageUrl} ...");
        AdBlockEngine.Instance.IsEnabled = false;
        AdBlockEngine.Instance.ResetCounters();

        var swHeavyOff = Stopwatch.StartNew();
        await NavigateAndWaitAsync(activeTab, heavyPageUrl, 25000);
        swHeavyOff.Stop();

        await Task.Delay(4000);
        var heavyOffSnapshot = ProcessMemoryTracker.CaptureSnapshot();
        var heavyOffResult = new BenchmarkResult
        {
            Scenario = "Yoğun Sayfa (AdBlock KAPALI)",
            Url = heavyPageUrl,
            AdBlockActive = false,
            HostWorkingSetMB = heavyOffSnapshot.HostWorkingSetMB,
            WebViewWorkingSetMB = heavyOffSnapshot.WebViewWorkingSetMB,
            TotalWorkingSetMB = heavyOffSnapshot.TotalWorkingSetMB,
            TotalPrivateMB = heavyOffSnapshot.TotalPrivateMB,
            ProcessCount = 1 + heavyOffSnapshot.WebViewProcesses.Count,
            BlockedRequests = 0,
            LoadDuration = swHeavyOff.Elapsed,
            Snapshot = heavyOffSnapshot
        };
        results.Add(heavyOffResult);
        _logger(heavyOffSnapshot.DetailedBreakdown());

        // -----------------------------------------------------------------
        // FAZ 3B: YOĞUN SAYFA (AdBlock AÇIK)
        // -----------------------------------------------------------------
        _logger($"\n[FAZ 3B/5] Yoğun Web Sayfası (AdBlock AÇIK): {heavyPageUrl} ...");
        AdBlockEngine.Instance.IsEnabled = true;
        AdBlockEngine.Instance.ResetCounters();

        await NavigateAndWaitAsync(activeTab, "about:blank", 5000);
        _tabManager.TrimMemory();
        await Task.Delay(1500);

        var swHeavyOn = Stopwatch.StartNew();
        await NavigateAndWaitAsync(activeTab, heavyPageUrl, 25000);
        swHeavyOn.Stop();

        await Task.Delay(4000);
        var heavyOnSnapshot = ProcessMemoryTracker.CaptureSnapshot();
        var heavyOnResult = new BenchmarkResult
        {
            Scenario = "Yoğun Sayfa (AdBlock AÇIK)",
            Url = heavyPageUrl,
            AdBlockActive = true,
            HostWorkingSetMB = heavyOnSnapshot.HostWorkingSetMB,
            WebViewWorkingSetMB = heavyOnSnapshot.WebViewWorkingSetMB,
            TotalWorkingSetMB = heavyOnSnapshot.TotalWorkingSetMB,
            TotalPrivateMB = heavyOnSnapshot.TotalPrivateMB,
            ProcessCount = 1 + heavyOnSnapshot.WebViewProcesses.Count,
            BlockedRequests = AdBlockEngine.Instance.BlockedRequests,
            LoadDuration = swHeavyOn.Elapsed,
            Snapshot = heavyOnSnapshot
        };
        results.Add(heavyOnResult);
        _logger(heavyOnSnapshot.DetailedBreakdown());
        _logger($"[AdBlock] Engellenen Reklam/İzleyici İsteği: {heavyOnResult.BlockedRequests}");

        // -----------------------------------------------------------------
        // FAZ 4: BELLEK KIRPMA / HİBERNATION OPTİMİZASYONU
        // -----------------------------------------------------------------
        _logger("\n[FAZ 4/5] Bellek Kırpma (TrimMemory & GC & Low Level)...");
        _tabManager.TrimMemory();
        await Task.Delay(2000);

        var trimmedSnapshot = ProcessMemoryTracker.CaptureSnapshot();
        var trimmedResult = new BenchmarkResult
        {
            Scenario = "Kırpma Sonrası (Post-Trim / Low Target)",
            Url = heavyPageUrl,
            AdBlockActive = true,
            HostWorkingSetMB = trimmedSnapshot.HostWorkingSetMB,
            WebViewWorkingSetMB = trimmedSnapshot.WebViewWorkingSetMB,
            TotalWorkingSetMB = trimmedSnapshot.TotalWorkingSetMB,
            TotalPrivateMB = trimmedSnapshot.TotalPrivateMB,
            ProcessCount = 1 + trimmedSnapshot.WebViewProcesses.Count,
            BlockedRequests = AdBlockEngine.Instance.BlockedRequests,
            Snapshot = trimmedSnapshot
        };
        results.Add(trimmedResult);
        _logger(trimmedSnapshot.DetailedBreakdown());

        // -----------------------------------------------------------------
        // FAZ 5: ÇOKLU SEKME VE HİBERNATION (5 SEKME AÇIK)
        // -----------------------------------------------------------------
        _logger("\n[FAZ 5/5] Çoklu Sekme Hibernation Testi (5 Sekme Açılıyor)...");
        var tab1 = await _tabManager.CreateTabAsync("https://example.com", false);
        var tab2 = await _tabManager.CreateTabAsync("https://httpbin.org/html", false);
        var tab3 = await _tabManager.CreateTabAsync("https://news.ycombinator.com", false);
        var tab4 = await _tabManager.CreateTabAsync("https://en.wikipedia.org", false);

        await _tabManager.SwitchToTabAsync(tab1);
        await Task.Delay(2500);
        _tabManager.TrimMemory();
        await Task.Delay(1500);

        var multiTabSnapshot = ProcessMemoryTracker.CaptureSnapshot();
        var multiTabResult = new BenchmarkResult
        {
            Scenario = "5 Sekme (4 Hibernated + 1 Aktif)",
            Url = "5 tabs pool",
            AdBlockActive = true,
            HostWorkingSetMB = multiTabSnapshot.HostWorkingSetMB,
            WebViewWorkingSetMB = multiTabSnapshot.WebViewWorkingSetMB,
            TotalWorkingSetMB = multiTabSnapshot.TotalWorkingSetMB,
            TotalPrivateMB = multiTabSnapshot.TotalPrivateMB,
            ProcessCount = 1 + multiTabSnapshot.WebViewProcesses.Count,
            BlockedRequests = AdBlockEngine.Instance.BlockedRequests,
            Snapshot = multiTabSnapshot
        };
        results.Add(multiTabResult);
        _logger(multiTabSnapshot.DetailedBreakdown());

        // Print final summary table
        _logger("\n" + GenerateSummaryTable(results));
        return results;
    }

    private static async Task<bool> NavigateAndWaitAsync(BrowserTab tab, string url, int timeoutMs)
    {
        if (tab.WebViewControl?.CoreWebView2 == null)
        {
            return false;
        }

        var tcs = new TaskCompletionSource<bool>();
        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                tab.WebViewControl.CoreWebView2.NavigationCompleted -= Handler;
            }
            catch { }
            tcs.TrySetResult(e.IsSuccess);
        }

        tab.WebViewControl.CoreWebView2.NavigationCompleted += Handler;
        tab.WebViewControl.CoreWebView2.Navigate(url);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        if (completedTask == tcs.Task)
        {
            return await tcs.Task;
        }
        else
        {
            try
            {
                tab.WebViewControl.CoreWebView2.NavigationCompleted -= Handler;
            }
            catch { }
            return false;
        }
    }

    public static string GenerateSummaryTable(List<BenchmarkResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("==========================================================================================");
        sb.AppendLine("                         PERFORMANS VE RAM TESTİ SONUÇ TABLOSU                           ");
        sb.AppendLine("==========================================================================================");
        sb.AppendLine(string.Format("{0,-32} | {1,-10} | {2,-11} | {3,-12} | {4,-10} | {5,-9}",
            "Test Senaryosu", "Host RAM", "WebView RAM", "TOPLAM RAM", "Commit", "Süreç"));
        sb.AppendLine("------------------------------------------------------------------------------------------");

        foreach (var r in results)
        {
            sb.AppendLine(string.Format("{0,-32} | {1,7:F1} MB | {2,8:F1} MB | {3,9:F1} MB | {4,7:F1} MB | {5,5} adet",
                r.Scenario,
                r.HostWorkingSetMB,
                r.WebViewWorkingSetMB,
                r.TotalWorkingSetMB,
                r.TotalPrivateMB,
                r.ProcessCount));
        }

        sb.AppendLine("==========================================================================================");
        return sb.ToString();
    }
}
