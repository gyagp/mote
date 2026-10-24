using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Mote;

public static class Theme
{
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is not int value || value == 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException) { return true; }
    }

    public static void Apply(string preference, ResourceDictionary resources)
    {
        var dark = preference == "dark" || (preference == "system" && IsSystemDark());
        var colors = dark ? new Dictionary<string, string>
        {
            ["Surface"] = "#202827", ["SurfaceHover"] = "#2C3834", ["Stroke"] = "#485B53",
            ["Ink"] = "#F1F5F3", ["Muted"] = "#99ABA2", ["Accent"] = "#9BE4C3",
            ["AccentSurface"] = "#2B4036", ["Panel"] = "#1B2220", ["Separator"] = "#34423B"
        } : new Dictionary<string, string>
        {
            ["Surface"] = "#F6F9F7", ["SurfaceHover"] = "#E8F2EC", ["Stroke"] = "#C7D6CC",
            ["Ink"] = "#243C30", ["Muted"] = "#607C6A", ["Accent"] = "#26734E",
            ["AccentSurface"] = "#DEEEE3", ["Panel"] = "#FAFCFA", ["Separator"] = "#D2DFD6"
        };
        foreach (var (key, value) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            resources[key] = brush;
        }
    }
}
