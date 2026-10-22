using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace Sikrowser.Core;

public record ProcessMemoryDetail(
    int ProcessId,
    string ProcessName,
    string Kind,
    long WorkingSetBytes,
    long PrivateMemoryBytes
)
{
    public double WorkingSetMB => WorkingSetBytes / (1024.0 * 1024.0);
    public double PrivateMemoryMB => PrivateMemoryBytes / (1024.0 * 1024.0);
}

public class MemorySnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public ProcessMemoryDetail HostProcess { get; init; } = null!;
    public List<ProcessMemoryDetail> WebViewProcesses { get; init; } = new();

    public long TotalWorkingSetBytes => HostProcess.WorkingSetBytes + WebViewProcesses.Sum(p => p.WorkingSetBytes);
    public long TotalPrivateBytes => HostProcess.PrivateMemoryBytes + WebViewProcesses.Sum(p => p.PrivateMemoryBytes);

    public double TotalWorkingSetMB => TotalWorkingSetBytes / (1024.0 * 1024.0);
    public double TotalPrivateMB => TotalPrivateBytes / (1024.0 * 1024.0);

    public double HostWorkingSetMB => HostProcess.WorkingSetMB;
    public double WebViewWorkingSetMB => WebViewProcesses.Sum(p => p.WorkingSetMB);

    public string ShortSummary =>
        $"Total RAM: {TotalWorkingSetMB:F1} MB (Host: {HostWorkingSetMB:F1} MB | WebView2 [{WebViewProcesses.Count} procs]: {WebViewWorkingSetMB:F1} MB)";

    public string DetailedBreakdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- RAM ÖLÇÜM RAPORU [{Timestamp:HH:mm:ss.fff}] ---");
        sb.AppendLine($"Host Process ({HostProcess.ProcessName} [PID: {HostProcess.ProcessId}]):");
        sb.AppendLine($"  - Working Set (Fiziksel): {HostProcess.WorkingSetMB:F2} MB");
        sb.AppendLine($"  - Private Bytes (Commit): {HostProcess.PrivateMemoryMB:F2} MB");

        sb.AppendLine($"WebView2 Alt Süreçleri ({WebViewProcesses.Count} adet):");
        foreach (var p in WebViewProcesses)
        {
            sb.AppendLine($"  [{p.Kind}] PID {p.ProcessId,5} ({p.ProcessName}): Working Set = {p.WorkingSetMB,6:F2} MB | Commit = {p.PrivateMemoryMB,6:F2} MB");
        }

        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"TOPLAM ÇALIŞMA KÜMESİ (Fiziksel RAM): {TotalWorkingSetMB:F2} MB");
        sb.AppendLine($"TOPLAM ÖZEL BELLEK (Private Commit):  {TotalPrivateMB:F2} MB");
        sb.AppendLine("--------------------------------------------------");
        return sb.ToString();
    }
}

/// <summary>
/// Comprehensive memory tracker that discovers and measures the host process
/// and all associated Microsoft WebView2 child processes (Browser, GPU, Renderer, Utility).
/// </summary>
public static class ProcessMemoryTracker
{
    /// <summary>
    /// Captures a complete snapshot of all processes involved in the browser session.
    /// </summary>
    public static MemorySnapshot CaptureSnapshot()
    {
        var host = Process.GetCurrentProcess();
        host.Refresh();

        var hostDetail = new ProcessMemoryDetail(
            host.Id,
            host.ProcessName,
            "Host (C# App)",
            host.WorkingSet64,
            host.PrivateMemorySize64
        );

        var webViewDetails = new List<ProcessMemoryDetail>();
        var trackedPids = new HashSet<int>();

        // Query WebView2 official environment API for child processes belonging to this environment
        try
        {
            var infos = BrowserEnvironmentManager.Instance.GetProcessInfos();
            foreach (var info in infos)
            {
                if (trackedPids.Add(info.ProcessId))
                {
                    var detail = TryGetProcessDetail(info.ProcessId, info.Kind.ToString());
                    if (detail != null)
                    {
                        webViewDetails.Add(detail);
                    }
                }
            }
        }
        catch
        {
            // Ignore environment query error if not yet initialized
        }

        return new MemorySnapshot
        {
            HostProcess = hostDetail,
            WebViewProcesses = webViewDetails
        };
    }

    private static ProcessMemoryDetail? TryGetProcessDetail(int pid, string kind)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Refresh();
            return new ProcessMemoryDetail(
                p.Id,
                p.ProcessName,
                kind,
                p.WorkingSet64,
                p.PrivateMemorySize64
            );
        }
        catch
        {
            return null;
        }
    }
}
