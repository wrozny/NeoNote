using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ModernNotepad.Core;
using Microsoft.Win32;

namespace ModernNotepad;

/// <summary>Applies matching client-area brushes and native Windows title-bar appearance.</summary>
internal static class WindowTheme
{
    /// <summary>Sets DWM attributes on Windows 10/11, falling back to the older Windows 10 attribute.</summary>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    /// <summary>Assigns explicit palette brushes and updates an existing native window.</summary>
    public static void Apply(Window window, bool dark)
        => Apply(window, dark ? "Dark" : "Light");

    /// <summary>Resolves system preferences while keeping named dark palettes distinct.</summary>
    public static ThemePalette Palette(string name) => AppThemes.Get(name,
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0);

    /// <summary>Applies a named palette to all client-area resources and native window chrome.</summary>
    public static void Apply(Window window, string theme)
    {
        var palette = Palette(theme);
        var colors = new[] { palette.Surface, palette.Panel, palette.Input, palette.Text, palette.Muted, palette.Stroke, palette.Hover, palette.Accent };
        var keys = new[] { "Surface", "Panel", "Input", "Text", "Muted", "Stroke", "Hover", "Accent" };
        for (var i = 0; i < keys.Length; i++)
            window.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        window.SetResourceReference(Window.BackgroundProperty, "Surface");
        window.SetResourceReference(Window.ForegroundProperty, "Text");
        window.Resources[SystemColors.ControlBrushKey] = window.Resources["Panel"];
        window.Resources[SystemColors.ControlTextBrushKey] = window.Resources["Text"];
        window.Resources[SystemColors.WindowBrushKey] = window.Resources["Input"];
        window.Resources[SystemColors.WindowTextBrushKey] = window.Resources["Text"];
        window.Resources[SystemColors.ScrollBarBrushKey] = window.Resources["Panel"];
        ApplyTitleBar(window, palette.IsDark);
    }

    /// <summary>Applies native caption colors once a window handle exists without replacing standard window buttons.</summary>
    public static void ApplyTitleBar(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var enabled = dark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) < 0)
            DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    /// <summary>Shares the owner's palette and styles with a modal settings or navigation window.</summary>
    public static void PrepareDialog(Window dialog, Window owner, bool dark)
    {
        dialog.Resources.MergedDictionaries.Add(owner.Resources);
        dialog.SetResourceReference(Window.BackgroundProperty, "Surface");
        dialog.SetResourceReference(Window.ForegroundProperty, "Text");
        dialog.SourceInitialized += (_, _) => ApplyTitleBar(dialog, dark);
    }
}
