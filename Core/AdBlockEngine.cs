using System.Collections.Concurrent;

namespace Sikrowser.Core;

/// <summary>
/// High-performance, modular ad and tracker blocking engine.
/// Evaluates network requests in O(1) time using hashed domain trie techniques.
/// Supports both file-based filter lists and dynamic allowlists.
/// </summary>
public sealed class AdBlockEngine
{
    private static readonly Lazy<AdBlockEngine> _instance =
        new(() => new AdBlockEngine());

    public static AdBlockEngine Instance => _instance.Value;

    private readonly HashSet<string> _blockedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _blockedUrlPatterns = new();

    private long _totalRequests;
    private long _blockedRequests;
    private long _allowedRequests;

    public bool IsEnabled { get; set; } = true;

    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long BlockedRequests => Interlocked.Read(ref _blockedRequests);
    public long AllowedRequests => Interlocked.Read(ref _allowedRequests);

    public event Action<Uri, bool, string>? RequestEvaluated;

    private AdBlockEngine()
    {
        InitializeDefaultRules();
        LoadCustomFilterFiles();
    }

    /// <summary>
    /// Evaluates whether a requested URI should be blocked.
    /// </summary>
    public bool ShouldBlock(Uri? uri, out string reason)
    {
        reason = string.Empty;
        if (!IsEnabled || uri == null)
        {
            return false;
        }

        Interlocked.Increment(ref _totalRequests);
        string host = uri.Host;

        // 1. Check Allowlist (whitelisted domains always pass)
        if (IsDomainAllowed(host))
        {
            Interlocked.Increment(ref _allowedRequests);
            RequestEvaluated?.Invoke(uri, false, "Allowed by user whitelist");
            return false;
        }

        // 2. Check Domain Blocklist (exact match and parent domain hierarchy)
        if (IsDomainBlocked(host, out string matchedDomain))
        {
            Interlocked.Increment(ref _blockedRequests);
            reason = $"Domain match: {matchedDomain}";
            RequestEvaluated?.Invoke(uri, true, reason);
            return true;
        }

        // 3. Check specific URL keyword patterns
        string absoluteUri = uri.AbsoluteUri;
        for (int i = 0; i < _blockedUrlPatterns.Count; i++)
        {
            if (absoluteUri.Contains(_blockedUrlPatterns[i], StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _blockedRequests);
                reason = $"Pattern match: {_blockedUrlPatterns[i]}";
                RequestEvaluated?.Invoke(uri, true, reason);
                return true;
            }
        }

        Interlocked.Increment(ref _allowedRequests);
        RequestEvaluated?.Invoke(uri, false, "Clean");
        return false;
    }

    private bool IsDomainAllowed(string host)
    {
        if (string.IsNullOrEmpty(host)) return false;

        string current = host;
        while (!string.IsNullOrEmpty(current))
        {
            if (_allowedDomains.Contains(current))
            {
                return true;
            }

            int dotIndex = current.IndexOf('.');
            if (dotIndex < 0 || dotIndex == current.Length - 1)
            {
                break;
            }
            current = current.Substring(dotIndex + 1);
        }

        return false;
    }

    private bool IsDomainBlocked(string host, out string matchedDomain)
    {
        matchedDomain = string.Empty;
        if (string.IsNullOrEmpty(host)) return false;

        string current = host;
        while (!string.IsNullOrEmpty(current))
        {
            if (_blockedDomains.Contains(current))
            {
                matchedDomain = current;
                return true;
            }

            int dotIndex = current.IndexOf('.');
            if (dotIndex < 0 || dotIndex == current.Length - 1)
            {
                break;
            }
            current = current.Substring(dotIndex + 1);
        }

        return false;
    }

    public void AddAllowDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return;
        lock (_allowedDomains)
        {
            _allowedDomains.Add(domain.Trim().ToLowerInvariant());
        }
    }

    public void RemoveAllowDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return;
        lock (_allowedDomains)
        {
            _allowedDomains.Remove(domain.Trim().ToLowerInvariant());
        }
    }

    public void AddBlockDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return;
        lock (_blockedDomains)
        {
            _blockedDomains.Add(domain.Trim().ToLowerInvariant());
        }
    }

    public void ResetCounters()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _blockedRequests, 0);
        Interlocked.Exchange(ref _allowedRequests, 0);
    }

    public int BlockedDomainsCount => _blockedDomains.Count;
    public int AllowedDomainsCount => _allowedDomains.Count;

    private void InitializeDefaultRules()
    {
        string[] defaultBlocked =
        {
            // Google Ad & Tracking Services
            "doubleclick.net", "adservice.google.com", "pagead2.googlesyndication.com",
            "googlesyndication.com", "google-analytics.com", "googleadservices.com",
            "googletagmanager.com", "googletagservices.com", "stats.g.doubleclick.net",
            "admob.com", "2mdn.net", "invitemedia.com",

            // Meta / Facebook Tracking & Ads
            "pixel.facebook.com", "an.facebook.com", "connect.facebook.net",

            // Major Ad Exchanges & Networks
            "criteo.com", "criteo.net",
            "adnxs.com", "ib.adnxs.com",
            "rubiconproject.com",
            "openx.net", "openx.com",
            "pubmatic.com",
            "casalemedia.com", "indexww.com",
            "smartadserver.com",
            "triplelift.com",
            "taboola.com", "taboolasyndication.com",
            "outbrain.com", "outbrainimg.com",
            "revcontent.com",
            "mgid.com",
            "yieldmo.com",
            "bidswitch.net",
            "media.net",
            "adroll.com",
            "adform.net",
            "teads.tv",
            "sovrn.com",
            "lijit.com",
            "infolinks.com",
            "amazon-adsystem.com",
            "aax.amazon-adsystem.com",
            "serving-sys.com",

            // Analytics, Telemetry & User Fingerprinting
            "scorecardresearch.com",
            "quantserve.com",
            "chartbeat.com", "chartbeat.net",
            "hotjar.com", "hotjar.io",
            "clarity.ms",
            "yandex.ru/metrika", "mc.yandex.ru", "an.yandex.ru",
            "segment.io", "segment.com",
            "branch.io",
            "appsflyer.com",
            "adjust.com",
            "mixpanel.com",
            "amplitude.com",
            "newrelic.com",
            "crazyegg.com",
            "optimizely.com",
            "mouseflow.com",
            "fullstory.com",
            "inspectlet.com",
            "luckyorange.com",
            "kissmetrics.com",

            // Common Malicious & Pop-up Ad Domains
            "popcash.net", "popads.net", "propellerads.com", "adcash.com",
            "trafficjunky.com", "exoclick.com", "juicyads.com",

            // Native Widget & Churn Networks
            "gemini.yahoo.com", "advertising.yahoo.com",
            "advertising.apple.com", "iadsdk.apple.com",

            // Video Ads & Video Syndication Platforms
            "connatix.com", "cdm.connatix.com",
            "spotxchange.com", "spotx.tv",
            "innovid.com", "flashtalking.com",
            "tremorhub.com", "unrulymedia.com",
            "vdo.ai", "anyclip.com",

            // Enterprise DMP, DSP & Audience Trackers
            "krxd.net", "demdex.net", "omtrdc.net",
            "liveramp.com", "rlcdn.com",
            "permutive.com", "permutive.app",
            "bounceexchange.com", "bouncex.net",
            "bombora.com", "adsafeprotected.com",
            "moatads.com", "moat.com",
            "undertone.com", "gumgum.com", "sonobi.com",
            "sharethrough.com", "contextweb.com"
        };

        foreach (var domain in defaultBlocked)
        {
            _blockedDomains.Add(domain);
        }

        // Common URL patterns
        _blockedUrlPatterns.Add("/pagead/js/adsbygoogle.js");
        _blockedUrlPatterns.Add("/gtag/js?");
        _blockedUrlPatterns.Add("/analytics.js");
        _blockedUrlPatterns.Add("fbevents.js");
        _blockedUrlPatterns.Add("/ads.js");
        _blockedUrlPatterns.Add("prebid.js");
    }

    private void LoadCustomFilterFiles()
    {
        try
        {
            string blocklistPath = Path.Combine(BrowserConfig.FiltersFolder, "blocklist.txt");
            if (File.Exists(blocklistPath))
            {
                foreach (var line in File.ReadLines(blocklistPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("!"))
                    {
                        continue;
                    }

                    if (trimmed.StartsWith("0.0.0.0 ") || trimmed.StartsWith("127.0.0.1 "))
                    {
                        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            _blockedDomains.Add(parts[1].ToLowerInvariant());
                            continue;
                        }
                    }

                    if (trimmed.StartsWith("||") && trimmed.EndsWith("^"))
                    {
                        string domain = trimmed.Substring(2, trimmed.Length - 3).Trim('/');
                        _blockedDomains.Add(domain.ToLowerInvariant());
                        continue;
                    }

                    _blockedDomains.Add(trimmed.ToLowerInvariant());
                }
            }
            else
            {
                File.WriteAllText(blocklistPath,
                    "# Sikrowser Custom Blocklist\n" +
                    "# Add domains, one per line (e.g. adserver.com, ||tracker.org^, 0.0.0.0 badsite.com)\n" +
                    "# Lines starting with # or ! are ignored.\n");
            }

            string allowlistPath = Path.Combine(BrowserConfig.FiltersFolder, "allowlist.txt");
            if (File.Exists(allowlistPath))
            {
                foreach (var line in File.ReadLines(allowlistPath))
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("#") && !trimmed.StartsWith("!"))
                    {
                        _allowedDomains.Add(trimmed.ToLowerInvariant());
                    }
                }
            }
            else
            {
                File.WriteAllText(allowlistPath,
                    "# Sikrowser Custom Allowlist\n" +
                    "# Add trusted domains to exempt from ad blocking, one per line (e.g. example.com)\n");
            }
        }
        catch
        {
            // Fallback cleanly to built-in rules
        }
    }
}
