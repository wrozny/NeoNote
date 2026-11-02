using System.Globalization;

namespace MarkdownPreview.Core;

/// <summary>Defines trusted host commands; document-provided JavaScript stays disabled.</summary>
public static class PreviewScripts
{
    /// <summary>Changes visual scale while preserving the page's scroll position rather than scrolling the wheel.</summary>
    public static string SetZoom(double factor)
    {
        if (!double.IsFinite(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        var scale = Math.Clamp(factor, 0.5, 3).ToString(CultureInfo.InvariantCulture);
        return "(() => { const x = window.scrollX, y = window.scrollY; " +
            $"document.documentElement.style.zoom = '{scale}'; " +
            "void document.documentElement.offsetHeight; window.scrollTo(x, y); " +
            "return {x: window.scrollX, y: window.scrollY}; })()";
    }
}
