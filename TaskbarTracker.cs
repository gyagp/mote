using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Mote;

internal sealed record TaskbarSnapshot(nint Handle, Native.Rect Bounds, Native.Rect Monitor, double Scale, bool Visible);

internal sealed class TaskbarTracker
{
    public TaskbarSnapshot? Current { get; private set; }
    public IReadOnlyList<Span> Occupied { get; private set; } = [];
    public bool HasScan { get; private set; }
    public string ScanStatus { get; private set; } = "pending";
    private bool _scanning;
    private DateTime _lastScan = DateTime.MinValue;
    private nint _scanHandle;
    private Native.Rect _scanBounds;

    public void Refresh()
    {
        var handle = Native.FindWindow("Shell_TrayWnd", null);
        if (handle == 0 || !Native.GetWindowRect(handle, out var bounds)) { Current = null; return; }
        var monitor = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(handle, 2), ref monitor)) { Current = null; return; }
        var scale = Math.Max(96, Native.GetDpiForWindow(handle)) / 96d;
        // Auto-hide leaves only a narrow reveal strip on-screen. Never cover that strip.
        var visibleHeight = Math.Min(bounds.Bottom, monitor.Monitor.Bottom) - Math.Max(bounds.Top, monitor.Monitor.Top);
        var visible = Native.IsWindowVisible(handle) && !Native.IsCloaked(handle)
            && bounds.Width > bounds.Height && visibleHeight >= 30 * scale
            && !Native.IsFullscreenApp(handle, monitor.Monitor);
        Current = new(handle, bounds, monitor.Monitor, scale, visible);
        if (_scanHandle != handle || !_scanBounds.Equals(bounds))
        {
            HasScan = false;
            Occupied = [];
        }
    }

    // Explorer accessibility can stall during restart; scan off the UI thread and allow just one pending scan.
    public async Task ScanAsync()
    {
        if (_scanning || Current is not { Visible: true } snapshot || DateTime.UtcNow - _lastScan < TimeSpan.FromSeconds(2)) return;
        _scanning = true;
        _lastScan = DateTime.UtcNow;
        try
        {
            var spans = await Task.Run(() => ReadOccupied(snapshot));
            if (Current is { } current && current.Handle == snapshot.Handle && current.Bounds.Equals(snapshot.Bounds))
            {
                Occupied = spans;
                HasScan = true;
                _scanHandle = snapshot.Handle;
                _scanBounds = snapshot.Bounds;
                ScanStatus = $"ok: {spans.Count} occupied spans";
            }
        }
        catch (Exception e) when (e is ElementNotAvailableException or COMException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            HasScan = false;
            ScanStatus = e.GetType().Name;
        }
        finally { _scanning = false; }
    }

    private static List<Span> ReadOccupied(TaskbarSnapshot snapshot)
    {
        var root = AutomationElement.FromHandle(snapshot.Handle);
        var condition = new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));
        var elements = root.FindAll(TreeScope.Descendants, condition);
        var occupied = new List<Span>();
        foreach (AutomationElement element in elements)
        {
            var rect = element.Current.BoundingRectangle;
            if (element.Current.IsOffscreen || rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;
            if (rect.Bottom <= snapshot.Bounds.Top || rect.Top >= snapshot.Bounds.Bottom) continue;
            occupied.Add(new(rect.Left, rect.Right));
        }
        // Protect the complete notification area, including clock and show-desktop target.
        var tray = Native.FindWindowEx(snapshot.Handle, 0, "TrayNotifyWnd", null);
        if (tray != 0 && Native.GetWindowRect(tray, out var trayRect) && trayRect.Width > 0)
            occupied.Add(new(trayRect.Left, snapshot.Bounds.Right));
        if (occupied.Count < 2) throw new InvalidOperationException("Taskbar buttons are not available yet.");
        return occupied;
    }
}
