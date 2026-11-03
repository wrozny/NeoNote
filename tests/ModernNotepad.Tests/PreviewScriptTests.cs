using System.Globalization;
using ModernNotepad.Core;
using Xunit;

namespace ModernNotepad.Tests;

public sealed class PreviewScriptTests
{
    /// <summary>Ensures locale-specific decimal separators cannot turn a scale into malformed JavaScript.</summary>
    [Fact]
    public void ZoomUsesInvariantNumbersAndPreservesOffsets()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var script = PreviewScripts.SetZoom(1.25);
            Assert.Contains("'1.25'", script);
            Assert.Contains("window.scrollTo(x, y)", script);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    /// <summary>Rejects non-finite values before they can reach the browser.</summary>
    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void ZoomRejectsNonFiniteFactors(double factor) => Assert.Throws<ArgumentOutOfRangeException>(() => PreviewScripts.SetZoom(factor));
}
