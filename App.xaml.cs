using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Mote;

public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--render"))
        {
            var index = Array.IndexOf(e.Args, "--render");
            var directory = index + 1 < e.Args.Length ? e.Args[index + 1] : "artifacts";
            PreviewRenderer.Render(Path.GetFullPath(directory));
            Shutdown();
            return;
        }

        _instance = new Mutex(true, @"Local\Mote.MachineName.Widget", out _ownsInstance);
        if (!_ownsInstance) { Shutdown(); return; }

        var widget = new WidgetWindow();
        MainWindow = widget;
        widget.Show();
        if (e.Args.Contains("--diagnose"))
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "diagnostics.json"), widget.GetDiagnostics());
                Shutdown();
            };
            timer.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
