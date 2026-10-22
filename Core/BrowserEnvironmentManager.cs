using Microsoft.Web.WebView2.Core;

namespace Sikrowser.Core;

/// <summary>
/// Thread-safe singleton managing the single shared CoreWebView2Environment.
/// Enforces that only ONE environment is created and reused across the entire application lifetime.
/// </summary>
public sealed class BrowserEnvironmentManager
{
    private static readonly Lazy<BrowserEnvironmentManager> _instance =
        new(() => new BrowserEnvironmentManager());

    public static BrowserEnvironmentManager Instance => _instance.Value;

    private CoreWebView2Environment? _environment;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private BrowserEnvironmentManager() { }

    /// <summary>
    /// Gets or asynchronously creates the single shared CoreWebView2Environment.
    /// </summary>
    public async Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        if (_environment != null)
        {
            return _environment;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_environment != null)
            {
                return _environment;
            }

            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: BrowserConfig.BuildBrowserArguments(),
                language: null,
                targetCompatibleBrowserVersion: null,
                allowSingleSignOnUsingOSPrimaryAccount: false
            );

            _environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: BrowserConfig.UserDataFolder,
                options: options
            );

            return _environment;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Returns the active environment instance if already created, or null.
    /// </summary>
    public CoreWebView2Environment? Environment => _environment;

    /// <summary>
    /// Gets all active WebView2 processes associated with this environment.
    /// </summary>
    public IReadOnlyList<CoreWebView2ProcessInfo> GetProcessInfos()
    {
        if (_environment == null)
        {
            return Array.Empty<CoreWebView2ProcessInfo>();
        }

        try
        {
            return _environment.GetProcessInfos();
        }
        catch
        {
            return Array.Empty<CoreWebView2ProcessInfo>();
        }
    }
}
