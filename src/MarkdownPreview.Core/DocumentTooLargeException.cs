namespace MarkdownPreview.Core;

/// <summary>Signals a size limit before allocating the document's text and parser structures.</summary>
public sealed class DocumentTooLargeException(long actualBytes) : IOException("This file exceeds the editor's safe opening limit.")
{
    public long ActualBytes { get; } = actualBytes;
}
