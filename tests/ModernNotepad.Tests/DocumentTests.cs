using System.Text;
using ModernNotepad.Core;
using Xunit;

namespace ModernNotepad.Tests;

public sealed class DocumentTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ModernNotepadTests", Guid.NewGuid().ToString("N"));

    /// <summary>Creates an isolated directory for each test instance.</summary>
    public DocumentTests() => Directory.CreateDirectory(directory);

    /// <summary>Removes only files owned by this test.</summary>
    public void Dispose() => Directory.Delete(directory, true);

    /// <summary>Verifies scratch identity, save-as context and subsequent edits.</summary>
    [Fact]
    public void SaveAsEstablishesIdentityAndDirtyState()
    {
        var document = new Document { Text = "# Hello\r\n" };
        Assert.True(document.IsDirty);
        Assert.Equal(FileFormat.Markdown, document.Format);
        var first = Path.Combine(directory, "first.md"); document.Save(first);
        Assert.Equal(first, document.Path); Assert.False(document.IsDirty);
        document.Text += "Changed"; Assert.True(document.IsDirty);
        var second = Path.Combine(directory, "second.md"); document.Save(second);
        Assert.Equal(second, document.Path); Assert.Equal("# Hello\r\n", File.ReadAllText(first));
        document.Text += " again"; document.Save(document.Path!);
        Assert.EndsWith(" again", File.ReadAllText(second));
        Assert.Single(Directory.GetFiles(directory, "first.md"));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    /// <summary>Checks BOM and line-ending preservation across an edit and atomic replacement.</summary>
    [Fact]
    public void SavePreservesUtf16AndCrLf()
    {
        var path = Path.Combine(directory, "polish.txt");
        File.WriteAllText(path, "Zażółć\r\ngęślą", Encoding.Unicode);
        var document = Document.Open(path); document.Text += " jaźń"; document.Save(path);
        Assert.Equal("CRLF", document.LineEnding);
        Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.Unicode.GetPreamble()));
        Assert.Equal(document.Text, File.ReadAllText(path, Encoding.Unicode));
    }

    /// <summary>Ensures failed writes never advance document identity or saved text.</summary>
    [Fact]
    public void FailedSaveRetainsScratchState()
    {
        var document = new Document { Text = "valuable edits" };
        Assert.Throws<DirectoryNotFoundException>(() => document.Save(Path.Combine(directory, "missing", "file.md")));
        Assert.Null(document.Path); Assert.True(document.IsDirty); Assert.Equal("", document.SavedText);
    }

    /// <summary>Checks detection of disk changes and deletions even when timestamps are unreliable.</summary>
    [Fact]
    public void ExternalChangesAreDetectedByContent()
    {
        var path = Path.Combine(directory, "test.json"); File.WriteAllText(path, "{}");
        var document = Document.Open(path); Assert.False(document.HasExternalChanges());
        File.WriteAllText(path, "[]"); Assert.True(document.HasExternalChanges());
        File.Delete(path); Assert.True(document.HasExternalChanges());
    }

    /// <summary>Checks automatic format detection independent of extension casing.</summary>
    [Theory]
    [InlineData("a.JSON", FileFormat.Json)] [InlineData("a.xml", FileFormat.Xml)]
    [InlineData("a.ini", FileFormat.Ini)] [InlineData("a.toml", FileFormat.Toml)]
    [InlineData("a.md", FileFormat.Markdown)] [InlineData("a.unknown", FileFormat.Text)]
    public void DetectsFileFormats(string path, FileFormat expected) => Assert.Equal(expected, Document.DetectFormat(path));

    /// <summary>Refuses a 61 MB file before allocating text or running parsers.</summary>
    [Fact]
    public void OversizedFilesAreRejectedBeforeReading()
    {
        var path = Path.Combine(directory, "large.json");
        using (var stream = File.Create(path)) stream.SetLength(61_000_000);
        var error = Assert.Throws<DocumentTooLargeException>(() => Document.Open(path));
        Assert.Equal(61_000_000, error.ActualBytes);
    }
}
