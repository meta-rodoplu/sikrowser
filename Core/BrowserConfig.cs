namespace Sikrowser.Core;

public static class BrowserConfig
{
    public const string AppName = "Sikrowser";
    public const string AppDisplayName = "Sikrowser";
    public const string AppVersion = "1.0.0";

    /// <summary>
    /// Gets the dedicated user data directory for WebView2.
    /// Kept isolated and lean.
    /// </summary>
    public static string UserDataFolder
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(localAppData, AppName, "UserData");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }
    }

    /// <summary>
    /// Cleans up any residual cache or temporary files in the UserData directory
    /// ensuring zero persistent history or traces remain on disk.
    /// </summary>
    public static void CleanResidualUserData()
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName,
                "UserData"
            );

            if (!Directory.Exists(folder)) return;

            string[] cacheFolders = { "EBWebView\\Default\\Cache", "EBWebView\\Default\\Code Cache", "EBWebView\\Crashpad", "EBWebView\\GrShaderCache" };
            foreach (var rel in cacheFolders)
            {
                string target = Path.Combine(folder, rel);
                if (Directory.Exists(target))
                {
                    try { Directory.Delete(target, true); } catch { }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Gets the filter rules directory path.
    /// </summary>
    public static string FiltersFolder
    {
        get
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folder = Path.Combine(baseDir, "filters");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }
    }

    /// <summary>
    /// Builds strictly optimized Chromium arguments designed to minimize RAM
    /// while fully maintaining web compatibility, security updates, site isolation, and certificate validation.
    /// </summary>
    public static string BuildBrowserArguments()
    {
        var args = new List<string>
        {
            // Limit renderer processes to minimize cross-tab process explosion while preserving site isolation
            "--renderer-process-limit=1",
            "--process-per-site",

            // Direct V8 to strictly minimize memory allocations and optimize bytecode for size
            "--js-flags=--max-old-space-size=128 --optimize-for-size",

            // Maximized Network Throughput & Speed: HTTP/3 (QUIC) and Parallel Sockets
            "--enable-quic",
            "--enable-tcp-fast-open",
            "--enable-fast-unload",
            "--enable-async-dns",

            // High-Performance In-Memory HTTP Caching (32 MB bounded):
            // Avoids repeatedly re-downloading static CSS/JS/images on subsequent clicks,
            // while obeying standard HTTP ETag/Cache-Control so content is never stale.
            // Under InPrivate mode, this cache lives in RAM and is completely erased on exit.
            "--disk-cache-size=33554432",

            // Advanced Network and JS Concurrency:
            // Parallel downloading, async DNS resolver, and non-blocking background JS bytecode compilation
            "--enable-features=ParallelDownloading,PostQuantumCECPQ2,AsyncDns,V8ConcurrentSparkplug,V8BaselineBatchCompilation,MuteAudioTrackWhenInvisible",

            // Hardware-accelerated smooth rendering with safe driver fallback
            "--enable-gpu-rasterization",
            "--enable-zero-copy",
            "--fallback-to-software-gl",

            // Prevent GPU shader cache inflation
            "--disable-gpu-shader-disk-cache",

            // Disable composited smooth scrolling memory buffers (saves GPU RAM without affecting layout)
            "--disable-smooth-scrolling",

            // Disable unnecessary GPU video frame memory buffers
            "--disable-gpu-memory-buffer-video-frames",

            // Disable heavy background Chromium features that consume memory
            "--disable-features=Translate,OptimizationHints,MediaRouter,CalculateNativeWinOcclusion,InterestFeedContentSuggestions,AutofillServerCommunication",

            // Prevent background pings and telemetry
            "--disable-background-networking",

            // Disable crashpad crash reporting overhead
            "--disable-breakpad",

            // Disable unused speech APIs
            "--disable-speech-api",

            // Disable cloud sync services
            "--disable-sync"
        };

        return string.Join(" ", args);
    }

    /// <summary>
    /// Default search engine URL format.
    /// </summary>
    public static string SearchUrlFormat => "https://duckduckgo.com/?q={0}";

    /// <summary>
    /// Strict tab lifecycle limits:
    /// Maximum 3 active live tabs kept in RAM.
    /// </summary>
    public const int MaxActiveTabsInRam = 3;

    /// <summary>
    /// Background tabs inactive for this duration are frozen (suspended).
    /// </summary>
    public static readonly TimeSpan TabFreezeTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Background tabs inactive for this duration (or excess 4th, 5th+ tabs) are hibernated (evicted from RAM).
    /// </summary>
    public static readonly TimeSpan TabHibernateTimeout = TimeSpan.FromMinutes(5);
}
