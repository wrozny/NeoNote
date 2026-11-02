using System.Text.Json;
using System.Xml;
using MarkdownPreview.Core;
using Xunit;

namespace MarkdownPreview.Tests;

public sealed class FileFormatterTests
{
    /// <summary>Verifies readable indentation without changing JSON values, key order or Unicode text.</summary>
    [Fact]
    public void JsonFormattingPreservesValuesAndLineEndingIntent()
    {
        const string text = "{\"name\":\"Zażółć\",\"items\":[1,true,null,{\"x\":2}]}\r\n";
        var formatted = FileFormatter.Format(text, FileFormat.Json);
        Assert.Contains("\r\n    \"name\": \"Zażółć\"", formatted);
        Assert.EndsWith("\r\n", formatted);
        using var original = JsonDocument.Parse(text);
        using var result = JsonDocument.Parse(formatted);
        Assert.Equal(original.RootElement.GetProperty("items")[3].GetProperty("x").GetInt32(), result.RootElement.GetProperty("items")[3].GetProperty("x").GetInt32());
        Assert.Equal(formatted, FileFormatter.Format(formatted, FileFormat.Json));
    }

    /// <summary>Preserves declarations, comments and mixed-content whitespace while indenting structural XML.</summary>
    [Fact]
    public void XmlFormattingPreservesCommentsAndMixedContent()
    {
        const string text = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root><!--note--><p>Hello <b>world</b> !</p><items><item /></items></root>";
        var formatted = FileFormatter.Format(text, FileFormat.Xml);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", formatted);
        Assert.Contains("<!--note-->", formatted);
        Assert.Contains("Hello <b>world</b> !", formatted);
        Assert.Contains("\n    <items>\n        <item", formatted);
        Assert.False(formatted.EndsWith('\n'));
        Assert.Equal(formatted, FileFormatter.Format(formatted, FileFormat.Xml));
    }

    /// <summary>Rejects malformed documents rather than replacing them with partial output.</summary>
    [Fact]
    public void InvalidDocumentsAreNotFormatted()
    {
        Assert.ThrowsAny<JsonException>(() => FileFormatter.Format("{\"a\":}", FileFormat.Json));
        Assert.Throws<XmlException>(() => FileFormatter.Format("<a>", FileFormat.Xml));
        Assert.Throws<NotSupportedException>(() => FileFormatter.Format("# notes", FileFormat.Markdown));
    }
}
