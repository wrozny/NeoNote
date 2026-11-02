using MarkdownPreview.Core;
using Xunit;

namespace MarkdownPreview.Tests;

public sealed class LinkContrastTests
{
    /// <summary>Enforces strong link readability on each dark editor, code-block and preview background.</summary>
    [Theory]
    [InlineData("Dark")] [InlineData("Midnight")] [InlineData("Graphite")]
    [InlineData("Forest")] [InlineData("Warm")] [InlineData("System")]
    public void DarkLinksMeetSevenToOneContrast(string name)
    {
        var theme = AppThemes.Get(name, true);
        foreach (var background in new[] { theme.Surface, theme.Panel, theme.Input })
        {
            var contrast = (Luminance(theme.Link) + 0.05) / (Luminance(background) + 0.05);
            Assert.True(contrast >= 7, $"{name} link contrast is only {contrast:F2}:1 on {background}.");
        }
    }

    /// <summary>Computes WCAG relative luminance from a standard sRGB hex color.</summary>
    private static double Luminance(string color)
    {
        var red = Linear(Convert.ToInt32(color.Substring(1, 2), 16) / 255.0);
        var green = Linear(Convert.ToInt32(color.Substring(3, 2), 16) / 255.0);
        var blue = Linear(Convert.ToInt32(color.Substring(5, 2), 16) / 255.0);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    /// <summary>Converts an sRGB component to linear light for accessibility contrast calculations.</summary>
    private static double Linear(double component) => component <= 0.04045 ? component / 12.92 : Math.Pow((component + 0.055) / 1.055, 2.4);
}
