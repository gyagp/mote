using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mote;

internal static class PreviewRenderer
{
    internal static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        var board = new Canvas { Width = 1280, Height = 800, Background = Brush("#111815") };
        Add(board, Label("mote", 20, "#A4E3C4", FontWeights.SemiBold), 64, 46);
        Add(board, Label("A LITTLE MORE LOCAL", 10, "#82968A"), 135, 57);
        Add(board, Label("这台电脑，抬眼就知道。", 38, "#F0F5F1", FontWeights.SemiBold), 62, 106);
        Add(board, Label("把机器名安放在任务栏，让每一台电脑都有清晰的身份。", 15, "#9AAC9F"), 65, 170);
        Add(board, Label("01  /  深色", 11, "#A7BAAE"), 66, 242);
        Add(board, Label("02  /  浅色", 11, "#A7BAAE"), 66, 426);
        Add(board, Label("03  /  右键菜单", 11, "#A7BAAE"), 876, 242);

        AddTaskbar(board, 64, 274, false);
        AddTaskbar(board, 64, 458, true);

        var menuWindow = new WidgetWindow();
        var menu = (Border)menuWindow.Menu.Child;
        menuWindow.Menu.Child = null;
        Theme.Apply("dark", menu.Resources);
        menuWindow.DarkTheme.SetResourceReference(Control.BackgroundProperty, "AccentSurface");
        Add(board, menu, 876, 274);

        Add(board, Label("小巧，但恰到好处。", 22, "#EFF5F1", FontWeights.SemiBold), 64, 619);
        Add(board, Label("32 px", 16, "#A4E3C4", FontWeights.SemiBold), 65, 669);
        Add(board, Label("轻巧高度", 12, "#91A699"), 65, 699);
        Add(board, Label("一键复制", 16, "#A4E3C4", FontWeights.SemiBold), 245, 669);
        Add(board, Label("点一下，名字就到手", 12, "#91A699"), 245, 699);
        Add(board, Label("自由安放", 16, "#A4E3C4", FontWeights.SemiBold), 468, 669);
        Add(board, Label("拖动定位，自动记住", 12, "#91A699"), 468, 699);
        Add(board, Label("WINDOWS 11  ·  NATIVE WIDGET", 10, "#647C6D"), 65, 759);

        Save(board, Path.Combine(directory, "mote-design.png"), 1.5);
        menuWindow.Close();

        foreach (var theme in new[] { "dark", "light" })
        {
            var view = new WidgetView();
            Theme.Apply(theme, view.Resources);
            view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = view.DesiredSize;
            view.Width = size.Width;
            Save(view, Path.Combine(directory, $"widget-{theme}.png"), 3);
        }
    }

    private static void AddTaskbar(Canvas board, double x, double y, bool light)
    {
        var surface = new Border
        {
            Width = 760, Height = 106, CornerRadius = new CornerRadius(14),
            Background = Brush(light ? "#E2E9E5" : "#1A231F"),
            BorderBrush = Brush(light ? "#D9E5DC" : "#2B3A31"), BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        var canvas = new Canvas();
        surface.Child = canvas;
        Add(canvas, Label("桌面", 10, light ? "#83998B" : "#536D5D"), 18, 12);
        var bar = new Canvas { Width = 758, Height = 48, Background = Brush(light ? "#F0F3F1" : "#252B28") };
        Add(canvas, bar, 0, 56);
        var windowsMark = new UniformGrid { Width = 17, Height = 17, Rows = 2, Columns = 2 };
        for (var i = 0; i < 4; i++) windowsMark.Children.Add(new Border { Margin = new Thickness(1), Background = Brush(light ? "#788E80" : "#7F9487") });
        Add(bar, windowsMark, 288, 15);
        var icons = new[] { "\uE721", "\uE8B7", "\uE774" };
        for (var i = 0; i < icons.Length; i++)
        {
            var icon = Label(icons[i], 18, light ? "#788E80" : "#7F9487");
            icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            Add(bar, icon, 334 + i * 46, 14);
        }
        var widget = new WidgetView();
        Theme.Apply(light ? "light" : "dark", widget.Resources);
        Add(bar, widget, 12, 8);
        Add(bar, Label("17:30", 11, light ? "#4E6758" : "#A6B8AD"), 684, 8);
        Add(bar, Label("2026/10/24", 9, light ? "#738779" : "#7E9688"), 658, 25);
        Add(board, surface, x, y);
        Add(board, Label(light ? "温润的浅色，跟随系统自然切换。" : "石墨灰底色 · 薄荷绿图标 · 克制的细边框", 12, "#82998A"), x + 2, y + 120);
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Label(string text, double size, string color, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal,
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI")
    };

    private static void Add(Canvas canvas, UIElement item, double x, double y)
    {
        Canvas.SetLeft(item, x);
        Canvas.SetTop(item, y);
        canvas.Children.Add(item);
    }

    private static void Save(FrameworkElement element, string path, double scale)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale),
            (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
