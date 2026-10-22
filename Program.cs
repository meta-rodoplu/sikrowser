using System.Runtime.InteropServices;
using Sikrowser.Benchmark;
using Sikrowser.Core;
using Sikrowser.UI;

namespace Sikrowser;

static class Program
{
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool AllocConsole();

    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Check if command line contains benchmark flag
        if (args.Length > 0 && (args[0] == "--benchmark" || args[0] == "-b" || args[0] == "--test"))
        {
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }

            Console.WriteLine("\n[Sikrowser] CLI Benchmark Modu Başlatılıyor...\n");
            RunCliBenchmark();
            return;
        }

        // Standard interactive GUI mode
        Application.Run(new MainForm());
    }

    private static void RunCliBenchmark()
    {
        var form = new Form
        {
            Width = 1000,
            Height = 700,
            ShowInTaskbar = false,
            WindowState = FormWindowState.Minimized
        };

        var container = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(container);

        var tabManager = new TabManager(container);
        var runner = new BenchmarkRunner(tabManager, line => Console.WriteLine(line));

        form.Load += async (s, e) =>
        {
            try
            {
                var results = await runner.RunAllBenchmarksAsync();
                Console.WriteLine("\n[Sikrowser] Benchmark başarıyla tamamlandı!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Sikrowser HATA] {ex.Message}");
            }
            finally
            {
                form.Close();
                Application.Exit();
            }
        };

        Application.Run(form);
    }
}
