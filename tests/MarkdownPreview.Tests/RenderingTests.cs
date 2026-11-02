using MarkdownPreview.Core;
using Xunit;

namespace MarkdownPreview.Tests;

public sealed class RenderingTests
{
    /// <summary>Exercises CommonMark, GFM extensions and safe embedded HTML together.</summary>
    [Fact]
    public void MarkdownRendersRichContent()
    {
        var html = Rendering.MarkdownBody("# Heading\n\n**bold** ~~removed~~\n\n- [x] Done\n\n| A | B |\n|---|---|\n|1|2|\n\n```json\n{}\n```\n\n<h1>Raw HTML</h1>\n\n<details><summary>More</summary>Text</details>\n\n![Local](image.png)");
        foreach (var expected in new[] { "<h1", "<strong>", "<del>", "<table>", "<pre>", "language-json", "<details>", "image.png", "checkbox" }) Assert.Contains(expected, html);
    }

    /// <summary>Checks that active HTML cannot escape into the preview.</summary>
    [Fact]
    public void MarkdownRemovesActiveContent()
    {
        var html = Rendering.MarkdownBody("<script>alert(1)</script><img src='x' onerror='alert(2)'><iframe src='https://example.com'></iframe><a href='javascript:alert(3)'>bad</a>");
        foreach (var forbidden in new[] { "<script", "onerror", "<iframe", "javascript:" }) Assert.DoesNotContain(forbidden, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("script-src 'none'", Rendering.Preview(html, true));
    }

    /// <summary>Ensures each structured format rejects an actual malformed document.</summary>
    [Theory]
    [InlineData(FileFormat.Json, "{\n\"x\": }")]
    [InlineData(FileFormat.Xml, "<a><b></a>")]
    [InlineData(FileFormat.Toml, "value = [")]
    [InlineData(FileFormat.Ini, "[unclosed")]
    public void MalformedDocumentsProduceLocations(FileFormat format, string text)
    {
        var diagnostics = Rendering.Validate(text, format);
        Assert.NotEmpty(diagnostics);
        var diagnostic = diagnostics.First();
        Assert.True(diagnostic.Line >= 1); Assert.True(diagnostic.Column >= 1);
    }

    /// <summary>Ensures representative valid documents are accepted by every parser.</summary>
    [Theory]
    [InlineData(FileFormat.Json, "{\"a\":[1,true,null]}")]
    [InlineData(FileFormat.Xml, "<a x='1'><b/></a>")]
    [InlineData(FileFormat.Toml, "[section]\nvalue = [1, 2]")]
    [InlineData(FileFormat.Ini, "; comment\n[section]\nvalue=1")]
    public void ValidDocumentsPass(FileFormat format, string text) => Assert.Empty(Rendering.Validate(text, format));
}
