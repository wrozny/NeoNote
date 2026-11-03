using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ModernNotepad.Core;

namespace ModernNotepad;

public partial class MainWindow
{
    /// <summary>Verifies preview bounds, out-of-window drags, visibility, formatting and large-file safeguards on Windows.</summary>
    private async Task VerifyEditingFeaturesAsync(string directory)
    {
        document.Save(document.Path!);
        var markdownPath = document.Path!;
        var initialPreviewWidth = PreviewColumn.ActualWidth;
        await WindowsInputProbe.DragOutsideAsync(Divider, true);
        if (PreviewColumn.ActualWidth < 279 || EditorColumn.ActualWidth < 259 || PreviewColumn.ActualWidth > initialPreviewWidth - 20)
            throw new InvalidOperationException("Dragging beyond the right window edge violated pane minimums.");
        await WindowsInputProbe.DragOutsideAsync(Divider, false);
        if (PreviewColumn.ActualWidth < 279 || EditorColumn.ActualWidth < 259)
            throw new InvalidOperationException("Dragging beyond the left window edge violated pane minimums.");

        var width = PreviewColumn.ActualWidth;
        var originalFont = preferences.Font; var originalSize = preferences.FontSize;
        preferences.Font = "Segoe UI"; preferences.FontSize = 28; ApplyPreferences(); UpdateLayout();
        if (Math.Abs(PreviewColumn.ActualWidth - width) > 2)
            throw new InvalidOperationException("Changing font resized the preview pane.");
        ClosePreviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); UpdateLayout();
        if (document.Format != FileFormat.Markdown || PreviewHost.Visibility != Visibility.Collapsed || PreviewColumn.ActualWidth > 1 || OpenPreviewButton.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Closing the preview did not reclaim its width or preserve Markdown format.");
        preferences.FontSize = 32; ApplyPreferences(); UpdateLayout();
        if (PreviewColumn.ActualWidth > 1) throw new InvalidOperationException("Font settings reopened a manually hidden preview.");
        document.Format = FileFormat.Json; ApplyPreferences();
        if (OpenPreviewButton.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Preview reopen button was shown for JSON.");
        document.Format = FileFormat.Markdown; ApplyPreferences();
        OpenPreviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); UpdateLayout();
        if (!PreviewVisible || PreviewColumn.ActualWidth < 279) throw new InvalidOperationException("Preview did not reopen at a safe width.");
        preferences.Font = originalFont; preferences.FontSize = originalSize; ApplyPreferences();

        foreach (var name in AppThemes.Names.Where(name => name != "System"))
        {
            preferences.Theme = name; ApplyPreferences();
            var palette = Palette;
            if (((SolidColorBrush)Editor.Background).Color != (Color)ColorConverter.ConvertFromString(palette.Surface))
                throw new InvalidOperationException("Editor did not use palette " + name);
            await VerifyPreviewPaletteAsync(palette);
            var link = (Color)ColorConverter.ConvertFromString(palette.Link);
            var brush = (SolidColorBrush)Editor.TextArea.TextView.LinkTextForegroundBrush;
            if (brush.Color != link) throw new InvalidOperationException("Editor hyperlinks did not use the palette's readable link color.");
            var actual = await Preview.CoreWebView2.ExecuteScriptAsync("getComputedStyle(document.querySelector('a')).color");
            if (actual != $"\"rgb({link.R}, {link.G}, {link.B})\"")
                throw new InvalidOperationException("Preview hyperlinks did not use the palette's readable link color.");
        }
        preferences.Theme = "Midnight"; ApplyPreferences();
        await VerifyPreviewPaletteAsync(Palette);
        foreach (var choice in new[] { MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel })
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var dialog = Application.Current.Windows.OfType<AppDialog>().Single();
                if (((SolidColorBrush)dialog.Background).Color != ((SolidColorBrush)Background).Color)
                    throw new InvalidOperationException("Save dialog did not inherit the selected dark palette.");
                var expected = choice switch { MessageBoxResult.Yes => T("Save").Replace("_", ""), MessageBoxResult.No => T("Discard"), _ => T("Cancel") };
                VisualChildren<Button>(dialog).Single(button => Equals(button.Content, expected)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            if (Ask(T("Unsaved"), true) != choice) throw new InvalidOperationException("Themed save dialog returned the wrong action.");
        }

        const string compact = "{\"name\":\"example\",\"items\":[1,2,3]}";
        var jsonPath = Path.Combine(directory, "format.json"); File.WriteAllText(jsonPath, compact);
        OpenPath(jsonPath); FormatDocument();
        if (!Editor.Text.Contains("\n    \"name\"", StringComparison.Ordinal) || !document.IsDirty || File.ReadAllText(jsonPath) != compact)
            throw new InvalidOperationException("Formatting did not indent JSON as an unsaved edit.");
        Editor.Undo();
        if (Editor.Text != compact) throw new InvalidOperationException("Formatting was not a single undoable operation.");
        Editor.Redo(); document.Save(jsonPath);

        var originalPath = document.Path;
        var hugePath = Path.Combine(directory, "oversized.json");
        using (var stream = File.Create(hugePath)) stream.SetLength(61_000_000);
        DismissNextInformationDialog(); OpenPath(hugePath);
        if (document.Path != originalPath) throw new InvalidOperationException("Oversized-file rejection changed the active document.");
        var largePath = Path.Combine(directory, "limited.json");
        File.WriteAllText(largePath, "{\"value\":\"" + new string('x', 1_100_000) + "\"}");
        OpenPath(largePath);
        if (!limitedEditing || Editor.SyntaxHighlighting is not null || CanFormat)
            throw new InvalidOperationException("Large-file safeguards did not disable expensive analysis.");
        var scrollbar = VisualChildren<ScrollBar>(Editor).First(bar => bar.Orientation == Orientation.Vertical);
        if (((SolidColorBrush)scrollbar.Background).Color == Colors.White)
            throw new InvalidOperationException("The editor scrollbar remained white in a dark theme.");
        OpenPath(markdownPath);
        await VerifyPreviewHeadingAsync("Wheel heading");
        Editor.Select(0, 0);
        var original = Editor.Text;
        await WindowsInputProbe.TypePolishSAsync(Editor.TextArea);
        if (Editor.Text != "ś" + original) throw new InvalidOperationException("AltGr+S did not type ś; a shortcut may have intercepted text input.");
        Editor.Undo();
        if (Editor.Text != original) throw new InvalidOperationException("Polish text input did not remain an ordinary undoable edit.");
    }

    /// <summary>Waits for actual browser CSS to reflect a newly selected palette.</summary>
    private async Task VerifyPreviewPaletteAsync(ThemePalette palette)
    {
        var color = (Color)ColorConverter.ConvertFromString(palette.Surface);
        var expected = $"\"rgb({color.R}, {color.G}, {color.B})\"";
        var limit = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limit)
        {
            if (!previewNavigationPending && await Preview.CoreWebView2.ExecuteScriptAsync("getComputedStyle(document.body).backgroundColor") == expected) return;
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Preview did not apply the selected color palette.");
    }

    /// <summary>Closes the next sample-file informational dialog during a controlled smoke test.</summary>
    private void DismissNextInformationDialog()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            Application.Current.Windows.OfType<AppDialog>().Single().Close()));
    }

    /// <summary>Enumerates actual WPF descendants for dialog and scrollbar assertions.</summary>
    private static IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in VisualChildren<T>(child)) yield return descendant;
        }
    }
}
