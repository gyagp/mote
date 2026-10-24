using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Mote;

public partial class WidgetWindow : Window
{
    private readonly Settings _settings = Settings.Load();
    private readonly TaskbarTracker _taskbar = new();
    private readonly DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromMilliseconds(1600) };
    private nint _handle;
    private Native.Point? _down;
    private double _dragOrigin;
    private bool _dragging;
    private bool _fallback;
    private bool _closing;
    private Native.Rect _lastPlacement;

    public WidgetWindow()
    {
        InitializeComponent();
        MenuMachineName.Text = Environment.MachineName;
        Theme.Apply(_settings.Theme, Application.Current.Resources);
        Widget.CopyButton.Click += CopyMachineName;
        Widget.PreviewMouseLeftButtonDown += PointerDown;
        Widget.PreviewMouseMove += PointerMove;
        Widget.PreviewMouseLeftButtonUp += PointerUp;
        Widget.LostMouseCapture += (_, _) => { _down = null; _dragging = false; };
        Widget.PreviewMouseRightButtonUp += (_, e) => { OpenMenu(); e.Handled = true; };
        _feedbackTimer.Tick += (_, _) => { _feedbackTimer.Stop(); Widget.ShowCopied(false); };
        _positionTimer.Tick += async (_, _) =>
        {
            UpdatePosition();
            await _taskbar.ScanAsync();
        };
        SystemEvents.UserPreferenceChanged += PreferencesChanged;
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            var style = Native.GetWindowLongPtr(_handle, Native.GwlExStyle).ToInt64();
            Native.SetWindowLongPtr(_handle, Native.GwlExStyle, new(style | Native.WsExToolWindow | Native.WsExNoActivate));
            HwndSource.FromHwnd(_handle)?.AddHook(WindowProc);
        };
        Loaded += async (_, _) =>
        {
            UpdatePosition();
            _positionTimer.Start();
            await _taskbar.ScanAsync();
            UpdatePosition();
        };
        Closed += (_, _) =>
        {
            _closing = true;
            _positionTimer.Stop();
            _feedbackTimer.Stop();
            SystemEvents.UserPreferenceChanged -= PreferencesChanged;
        };
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021) { handled = true; return new nint(3); } // MA_NOACTIVATE
        if (message is 0x007E or 0x02E0) // display/DPI changes: let WPF process before repositioning
            Dispatcher.BeginInvoke(new Action(UpdatePosition), DispatcherPriority.Loaded);
        return 0;
    }

    private void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closing && _settings.Theme == "system") Theme.Apply("system", Application.Current.Resources);
        }));

    private void UpdatePosition()
    {
        if (_closing || _handle == 0 || _dragging) return;
        _taskbar.Refresh();
        if (_taskbar.Current is not { Visible: true } bar)
        {
            Menu.IsOpen = false;
            if (IsVisible) Hide();
            return;
        }

        var width = (int)Math.Ceiling(Widget.ActualWidth * bar.Scale);
        var height = (int)Math.Ceiling(Widget.ActualHeight * bar.Scale);
        if (width <= 0 || height <= 0) return;
        var start = bar.Bounds.Left + 12 * bar.Scale;
        var end = bar.Bounds.Right - 12 * bar.Scale;
        var x = _settings.Position is { } saved ? Placement.FromFraction(start, end, width, saved)
            : _taskbar.HasScan ? Placement.FindFreeSlot(start, end, width, _taskbar.Occupied, 12 * bar.Scale) : null;
        _fallback = x is null;
        // If Explorer cannot expose enough room, stay immediately above the taskbar instead of covering controls.
        x ??= start;
        var topEdge = bar.Bounds.Top <= bar.Monitor.Top;
        var y = _fallback ? (topEdge ? bar.Bounds.Bottom + 8 * bar.Scale : bar.Bounds.Top - height - 8 * bar.Scale)
            : bar.Bounds.Top + (bar.Bounds.Height - height) / 2d;
        var placement = new Native.Rect { Left = (int)Math.Round(x.Value), Top = (int)Math.Round(y), Right = width, Bottom = height };
        var wasHidden = !IsVisible;
        if (wasHidden) Show();
        // Refresh topmost ordering without activating or moving focus away from the user's application.
        if (!Menu.IsOpen)
        {
            var same = placement.Equals(_lastPlacement) && !wasHidden;
            Native.SetWindowPos(_handle, Native.HwndTopmost, placement.Left, placement.Top, width, height,
                Native.SwpNoActivate | (same ? Native.SwpNoSize | Native.SwpNoMove : 0));
        }
        _lastPlacement = placement;
    }

    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.Locked || !Native.GetCursorPos(out var point)) return;
        _down = point;
        Native.GetWindowRect(_handle, out var bounds);
        _dragOrigin = bounds.Left;
    }

    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (_down is not { } down || e.LeftButton != MouseButtonState.Pressed || _taskbar.Current is not { } bar) return;
        if (!Native.GetCursorPos(out var point)) return;
        var delta = point.X - down.X;
        if (!_dragging && Math.Abs(delta) < 5 * bar.Scale) return;
        _dragging = true;
        Menu.IsOpen = false;
        var width = (int)Math.Ceiling(Widget.ActualWidth * bar.Scale);
        var height = (int)Math.Ceiling(Widget.ActualHeight * bar.Scale);
        var start = bar.Bounds.Left + 12 * bar.Scale;
        var end = bar.Bounds.Right - 12 * bar.Scale;
        var x = Math.Clamp(_dragOrigin + delta, start, Math.Max(start, end - width));
        Native.SetWindowPos(_handle, Native.HwndTopmost, (int)Math.Round(x), bar.Bounds.Top + (bar.Bounds.Height - height) / 2,
            width, height, Native.SwpNoActivate);
        _settings.Position = Placement.ToFraction(start, end, width, x);
    }

    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        var dragged = _dragging;
        _down = null;
        _dragging = false;
        if (!dragged) return;
        e.Handled = true;
        Widget.CopyButton.ReleaseMouseCapture();
        SaveSettings();
        UpdatePosition();
    }

    private void CopyMachineName(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetDataObject(Environment.MachineName, true);
            Widget.ShowCopied(true);
            Menu.IsOpen = false;
            _feedbackTimer.Stop();
            _feedbackTimer.Start();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            OpenMenu();
            MenuHint.Text = "剪贴板暂时忙，请再试一次";
        }
    }

    private void OpenMenu()
    {
        if (_taskbar.Current is { } bar)
            Menu.Placement = bar.Bounds.Top <= bar.Monitor.Top
                ? System.Windows.Controls.Primitives.PlacementMode.Bottom : System.Windows.Controls.Primitives.PlacementMode.Top;
        MenuHint.Text = _fallback ? "任务栏空间不足时，显示在上方" : "点击复制 · 拖动调整位置";
        UpdateMenu();
        Menu.IsOpen = true;
        Dispatcher.BeginInvoke(new Action(() => MenuCopy.Focus()), DispatcherPriority.Input);
    }

    private void UpdateMenu()
    {
        foreach (var button in new[] { SystemTheme, DarkTheme, LightTheme })
        {
            var selected = (string)button.Tag == _settings.Theme;
            button.SetResourceReference(BackgroundProperty, selected ? "AccentSurface" : "Panel");
            button.SetResourceReference(ForegroundProperty, selected ? "Accent" : "Muted");
        }
        LockState.Text = _settings.Locked ? "开" : "关";
    }

    private void ChangeTheme(object sender, RoutedEventArgs e)
    {
        _settings.Theme = (string)((Button)sender).Tag;
        Theme.Apply(_settings.Theme, Application.Current.Resources);
        UpdateMenu();
        SaveSettings();
    }

    private void ToggleLock(object sender, RoutedEventArgs e) { _settings.Locked = !_settings.Locked; UpdateMenu(); SaveSettings(); }
    private void MenuKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { Menu.IsOpen = false; e.Handled = true; } }
    private void ResetPosition(object sender, RoutedEventArgs e) { _settings.Position = null; SaveSettings(); Menu.IsOpen = false; UpdatePosition(); }
    private void Quit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    private void SaveSettings() { if (!_settings.Save()) MenuHint.Text = "本次已应用，但未能保存设置"; }

    internal string GetDiagnostics()
    {
        Native.GetWindowRect(_handle, out var rect);
        return JsonSerializer.Serialize(new
        {
            machineName = Environment.MachineName,
            visible = IsVisible,
            fallback = _fallback,
            theme = _settings.Theme,
            scan = _taskbar.ScanStatus,
            occupied = _taskbar.Occupied,
            window = new { rect.Left, rect.Top, rect.Width, rect.Height },
            taskbar = _taskbar.Current is { } b ? new { b.Bounds.Left, b.Bounds.Top, b.Bounds.Width, b.Bounds.Height, b.Scale, b.Visible } : null
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
