using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MarkdownPreview;

/// <summary>Applies matching client-area brushes and native Windows title-bar appearance.</summary>
internal static class WindowTheme
{
    /// <summary>Sets DWM attributes on Windows 10/11, falling back to the older Windows 10 attribute.</summary>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    /// <summary>Assigns explicit palette brushes and updates an existing native window.</summary>
    public static void Apply(Window window, bool dark)
    {
        var colors = dark
            ? new[] { "#1E2026", "#252832", "#191B21", "#E5E7EB", "#949CAC", "#373C49", "#353C4D", "#82AAFF" }
            : new[] { "#FAFBFD", "#F0F2F6", "#FFFFFF", "#202632", "#657086", "#D8DDE7", "#E2E8F3", "#245DC1" };
        var keys = new[] { "Surface", "Panel", "Input", "Text", "Muted", "Stroke", "Hover", "Accent" };
        for (var i = 0; i < keys.Length; i++)
            window.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        window.SetResourceReference(Window.BackgroundProperty, "Surface");
        window.SetResourceReference(Window.ForegroundProperty, "Text");
        ApplyTitleBar(window, dark);
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
        Apply(dialog, dark);
        dialog.SourceInitialized += (_, _) => ApplyTitleBar(dialog, dark);
    }
}
