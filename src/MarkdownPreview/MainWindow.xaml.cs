using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MarkdownPreview.Core;

namespace MarkdownPreview;

/// <summary>Coordinates window commands while delegating document and parsing work to the core.</summary>
public partial class MainWindow : Window
{
    private Document document = new();
    private Preferences preferences = Preferences.Load();
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly EditorHighlighting markers = new();
    private readonly SemaphoreSlim parserGate = new(1, 1);
    private bool webReady;
    private bool webStarting;
    private bool closing;
    private int revision;
    private string? mappedFolder;
    private bool checkingExternal;
    private double zoom = 1;
    private bool previewNavigationPending;
    private ulong activePreviewNavigation;
    private readonly List<string> previewTrace = [];
    private bool previewHidden;
    private bool previewLayoutVisible;
    private double previewShare = 0.5;
    private bool limitedEditing;
    private MenuItem? formatDocumentItem;
    private ThemePalette Palette => WindowTheme.Palette(preferences.Theme);
    private bool Dark => Palette.IsDark;
    private bool PreviewVisible => document.Format == FileFormat.Markdown && !previewHidden && !limitedEditing;
    private bool CanFormat => FileFormatter.Supports(document.Format) && Editor.Document.TextLength <= 1_000_000;

    /// <summary>Connects editor events and creates the initial document UI.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Editor.TextArea.TextView.LineTransformers.Add(markers);
        foreach (var margin in Editor.TextArea.LeftMargins.OfType<ICSharpCode.AvalonEdit.Editing.LineNumberMargin>())
            margin.Margin = new Thickness(0, 0, 8, 0);
        Editor.TextArea.LeftMargins.Add(new Border { Width = 8, IsHitTestVisible = false });
        Editor.TextChanged += OnTextChanged;
        Editor.TextArea.Caret.PositionChanged += OnCaretChanged;
        refreshTimer.Tick += OnRefreshTick;
        Closing += OnClosing;
        Activated += OnActivated;
        Drop += OnDrop;
        PreviewKeyDown += OnKeyDown;
        PreviewMouseWheel += OnZoomMouseWheel;
        SourceInitialized += OnSourceInitialized;
        FindButton.Click += OnFind;
        ReplaceButton.Click += OnReplace;
        ReplaceAllButton.Click += OnReplaceAll;
        CloseSearchButton.Click += OnCloseSearch;
        RetryPreviewButton.Click += OnRetryPreview;
        ClosePreviewButton.Click += OnClosePreview;
        OpenPreviewButton.Click += OnOpenPreview;
        Divider.DragCompleted += OnDividerDragCompleted;
        Loaded += OnLoaded;
        ApplyPreferences();
        UpdateStatus();
    }

    /// <summary>Returns the translated UI string.</summary>
    private string T(string key) => Strings.Get(key, preferences.Language);

    /// <summary>Applies colors, fonts, menus and format highlighting.</summary>
    private void ApplyPreferences()
    {
        WindowTheme.Apply(this, preferences.Theme);
        Editor.Background = Background;
        Editor.Foreground = Foreground;
        Editor.TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Palette.Link));
        Editor.TextArea.TextView.LinkTextBackgroundBrush = Brushes.Transparent;
        Editor.LineNumbersForeground = (Brush)Resources["Muted"];
        Editor.FontFamily = new FontFamily(preferences.Font);
        Editor.FontSize = Math.Clamp(preferences.FontSize, 8, 72) * zoom;
        Editor.WordWrap = preferences.WordWrap;
        UpdateEditingMode(true);
        Editor.TextArea.Caret.CaretBrush = Foreground;
        EditorHeading.Text = T("Editor"); PreviewHeading.Text = T("Preview");
        RetryPreviewButton.Content = T("Retry");
        ClosePreviewButton.ToolTip = T("ClosePreview");
        System.Windows.Automation.AutomationProperties.SetName(ClosePreviewButton, T("ClosePreview"));
        OpenPreviewButton.Content = T("OpenPreview");
        BuildMenu();
        FindButton.Content = T("Next"); ReplaceButton.Content = T("Replace").Replace("_", ""); ReplaceAllButton.Content = T("ReplaceAll");
        SearchBox.ToolTip = T("Find").Replace("_", ""); ReplaceBox.ToolTip = T("Replace").Replace("_", "");
        UpdateFormatVisibility();
        UpdateStatus();
        ScheduleRefresh();
    }

    /// <summary>Adds a translated command to a menu.</summary>
    private MenuItem Item(ItemsControl parent, string key, Action action, string shortcut = "")
    {
        var item = new MenuItem { Header = T(key), InputGestureText = shortcut };
        item.Click += (_, _) => Execute(action);
        parent.Items.Add(item);
        return item;
    }

    /// <summary>Creates a translated top-level menu.</summary>
    private MenuItem Menu(string key)
    {
        var menu = new MenuItem { Header = T(key) };
        MainMenu.Items.Add(menu);
        return menu;
    }

    /// <summary>Builds standard Notepad commands and application-specific format controls.</summary>
    private void BuildMenu()
    {
        MainMenu.Items.Clear();
        var file = Menu("File");
        Item(file, "New", NewDocument, "Ctrl+N"); Item(file, "Window", NewWindow, "Ctrl+Shift+N");
        Item(file, "Open", OpenDialog, "Ctrl+O"); file.Items.Add(new Separator());
        Item(file, "Save", () => Save(false), "Ctrl+S"); Item(file, "SaveAs", () => Save(true), "Ctrl+Shift+S");
        file.Items.Add(new Separator()); Item(file, "Exit", Close, "Alt+F4");
        var edit = Menu("Edit");
        Item(edit, "Undo", () => Editor.Undo(), "Ctrl+Z"); Item(edit, "Redo", () => Editor.Redo(), "Ctrl+Y"); edit.Items.Add(new Separator());
        Item(edit, "Cut", Editor.Cut, "Ctrl+X"); Item(edit, "Copy", Editor.Copy, "Ctrl+C"); Item(edit, "Paste", Editor.Paste, "Ctrl+V");
        Item(edit, "Delete", DeleteSelection, "Del"); edit.Items.Add(new Separator());
        Item(edit, "Find", ShowSearch, "Ctrl+F"); Item(edit, "Next", () => Find(false), "F3");
        Item(edit, "Previous", () => Find(true), "Shift+F3"); Item(edit, "Replace", ShowSearch, "Ctrl+H");
        Item(edit, "GoTo", GoToLine, "Ctrl+G"); Item(edit, "SelectAll", Editor.SelectAll, "Ctrl+A");
        Item(edit, "Date", InsertDate, "F5"); edit.Items.Add(new Separator());
        var formats = new MenuItem { Header = T("FileFormat") }; edit.Items.Add(formats);
        foreach (var format in Enum.GetValues<FileFormat>())
        {
            var item = new MenuItem { Header = format == FileFormat.Text ? T("Text") : format.ToString(), IsCheckable = true, IsChecked = document.Format == format };
            item.Click += (_, _) => { document.Format = format; ApplyPreferences(); UpdateStatus(); };
            formats.Items.Add(item);
        }
        var layout = Menu("Format");
        var wrap = Item(layout, "Wrap", ToggleWrap); wrap.IsCheckable = true; wrap.IsChecked = preferences.WordWrap;
        formatDocumentItem = Item(layout, "FormatDocument", FormatDocument, "Ctrl+Shift+F");
        formatDocumentItem.IsEnabled = CanFormat;
        formatDocumentItem.ToolTip = T("FormattingSupport");
        Item(MainMenu, "Settings", ShowSettings);
    }

    /// <summary>Runs an operation and reports recoverable IO and runtime failures.</summary>
    private void Execute(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { Inform(error is DocumentTooLargeException ? T("FileTooLarge") : error.Message, T("Error")); }
    }

    /// <summary>Shows a file picker after resolving unsaved edits.</summary>
    private void OpenDialog()
    {
        var dialog = new OpenFileDialog { Filter = "Markdown|*.md;*.markdown|JSON|*.json|XML|*.xml|INI|*.ini;*.cfg|TOML|*.toml|" + T("AllFiles") + "|*.*" };
        if (dialog.ShowDialog(this) == true) OpenPath(dialog.FileName);
    }

    /// <summary>Loads a selected file without discarding the current document on cancellation or read failure.</summary>
    public void OpenPath(string path) => Execute(() =>
    {
        if (!ConfirmDiscard()) return;
        var opened = Document.Open(path);
        document = opened;
        Editor.Text = document.Text;
        Editor.Document.UndoStack.ClearAll();
        ApplyPreferences(); UpdateStatus();
    });

    /// <summary>Resets the current window after unsaved-change confirmation.</summary>
    private void NewDocument()
    {
        if (!ConfirmDiscard()) return;
        document = new(); previewHidden = false; Editor.Text = ""; Editor.Document.UndoStack.ClearAll(); ApplyPreferences(); UpdateStatus();
    }

    /// <summary>Creates an independent window in the current process.</summary>
    private void NewWindow() => new MainWindow().Show();

    /// <summary>Asks whether pending edits should be saved, discarded, or retained.</summary>
    private bool ConfirmDiscard()
    {
        if (!document.IsDirty || document.Path is null && document.Text.Length == 0) return true;
        return Ask(T("Unsaved"), true) switch
        { MessageBoxResult.Yes => Save(false), MessageBoxResult.No => true, _ => false };
    }

    /// <summary>Saves the current document, warning about external modifications before overwriting.</summary>
    private bool Save(bool saveAs)
    {
        var path = document.Path;
        if (saveAs || path is null)
        {
            var extension = document.Format switch { FileFormat.Markdown => "md", FileFormat.Json => "json", FileFormat.Xml => "xml", FileFormat.Ini => "ini", FileFormat.Toml => "toml", _ => "txt" };
            var dialog = new SaveFileDialog { FileName = path is null ? "" : Path.GetFileName(path), DefaultExt = extension,
                Filter = $"{document.Format}|*.{extension}|{T("AllFiles")}|*.*", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }
        if (path == document.Path && document.HasExternalChanges() && Ask(T("External")) != MessageBoxResult.Yes) return false;
        document.Save(path); UpdateStatus(); ScheduleRefresh(); return true;
    }

    /// <summary>Prevents closing when saving was cancelled or failed.</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        try { e.Cancel = !ConfirmDiscard(); }
        catch (Exception error) { e.Cancel = true; Inform(error.Message, T("Error")); }
        if (e.Cancel) return;
        closing = true; revision++; refreshTimer.Stop(); Preview.Dispose();
    }

    /// <summary>Offers to reload externally modified files while preserving local edits when declined.</summary>
    private void OnActivated(object? sender, EventArgs e) => Execute(() =>
    {
        if (checkingExternal || document.Path is null || !File.Exists(document.Path) || !document.HasExternalChanges()) return;
        checkingExternal = true;
        try
        {
            if (Ask(T("Reload")) != MessageBoxResult.Yes) return;
            OpenPath(document.Path);
        }
        finally { checkingExternal = false; }
    });

    /// <summary>Opens the first dropped file through the normal unsaved-change flow.</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths) { e.Handled = true; OpenPath(paths[0]); }
    }

    /// <summary>Synchronizes edited text and invalidates pending background results.</summary>
    private void OnTextChanged(object? sender, EventArgs e)
    {
        document.Text = Editor.Text;
        var previous = limitedEditing;
        UpdateEditingMode();
        if (previous != limitedEditing) { BuildMenu(); UpdateFormatVisibility(); }
        if (formatDocumentItem is not null) formatDocumentItem.IsEnabled = CanFormat;
        UpdateStatus(); ScheduleRefresh();
    }

    /// <summary>Refreshes cursor information without reparsing text.</summary>
    private void OnCaretChanged(object? sender, EventArgs e) => UpdateStatus();

    /// <summary>Updates window identity and concise document information.</summary>
    private void UpdateStatus()
    {
        Title = (document.IsDirty ? "*" : "") + (document.Path is null ? T("Untitled") : Path.GetFileName(document.Path)) + " - NeoNote";
        StatusText.Text = $"{T("Line")} {Editor.TextArea.Caret.Line}   {T("Column")} {Editor.TextArea.Caret.Column}    |    {document.Format}    |    {document.Encoding.WebName}    |    {document.LineEnding}    |    {T("Errors")}: {markers.Diagnostics.Count}";
        StatusText.ToolTip = markers.Diagnostics.Count == 0 ? T("DiagnosticHint") : string.Join("\n", markers.Diagnostics.Take(30).Select(d => $"{d.Line}:{d.Column} {d.Message}"));
        ZoomText.Text = $"{zoom:P0}";
        ZoomText.ToolTip = T("ZoomHint");
        if (limitedEditing) { StatusText.Text += "    |    " + T("LargeMode"); StatusText.ToolTip = T("LargeModeHint"); }
    }

    /// <summary>Debounces parser work and invalidates previous results immediately.</summary>
    private void ScheduleRefresh() { revision++; refreshTimer.Stop(); refreshTimer.Start(); }

    /// <summary>Runs validation and rendering away from the UI thread, ignoring stale completions.</summary>
    private async void OnRefreshTick(object? sender, EventArgs e)
    {
        refreshTimer.Stop();
        var current = revision; var text = document.Text; var format = document.Format; var dark = Dark;
        try
        {
            if (limitedEditing)
            {
                markers.Diagnostics = []; Editor.TextArea.TextView.Redraw(); UpdateStatus(); return;
            }
            await parserGate.WaitAsync();
            (IReadOnlyList<Diagnostic> Errors, string Body) result;
            try
            {
                if (closing || current != revision) return;
                var showPreview = PreviewVisible;
                result = await Task.Run(() => (Rendering.Validate(text, format), showPreview ? Rendering.MarkdownBody(text, dark) : ""));
            }
            finally { parserGate.Release(); }
            if (closing || current != revision) return;
            markers.Diagnostics = result.Errors; Editor.TextArea.TextView.Redraw(); UpdateStatus();
            TracePreview($"Rendered revision {current}, HTML length {result.Body.Length}, ready={webReady}.");
            if (PreviewVisible && webReady) NavigatePreview(result.Body);
        }
        catch (Exception error) { if (!closing && current == revision) ShowPreviewFailure(error.Message); }
    }

    /// <summary>Creates WebView lazily so ordinary text files do not require browser startup.</summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateFormatVisibility();
        await EnsurePreview();
    }

    /// <summary>Switches the split view according to the selected format.</summary>
    private async void UpdateFormatVisibility()
    {
        var visible = PreviewVisible;
        OpenPreviewButton.Visibility = document.Format == FileFormat.Markdown && previewHidden && !limitedEditing ? Visibility.Visible : Visibility.Collapsed;
        PreviewHost.Visibility = Divider.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible != previewLayoutVisible)
        {
            // Reset both star columns together only when visibility changes, never during font/theme updates.
            PreviewColumn.MinWidth = visible ? 280 : 0;
            EditorColumn.Width = new GridLength(visible ? 1 - previewShare : 1, GridUnitType.Star);
            PreviewColumn.Width = visible ? new GridLength(previewShare, GridUnitType.Star) : new GridLength(0);
            DividerColumn.Width = new GridLength(visible ? 5 : 0);
            previewLayoutVisible = visible;
        }
        if (visible) Preview.Visibility = Visibility.Visible;
        if (IsLoaded && visible) await EnsurePreview();
    }

    /// <summary>Initializes an isolated browser profile and disables active document capabilities.</summary>
    private async Task EnsurePreview()
    {
        if (webReady || webStarting || closing || !PreviewVisible) return;
        webStarting = true;
        Preview.Visibility = Visibility.Visible;
        Preview.DefaultBackgroundColor = Dark ? System.Drawing.Color.FromArgb(30, 32, 38) : System.Drawing.Color.FromArgb(250, 251, 253);
        // Keep the composed browser unobscured while its graphics surface initializes.
        PreviewNotice.Visibility = Visibility.Collapsed;
        PreviewHeading.Text = T("Loading");
        RetryPreviewButton.Visibility = Visibility.Collapsed;
        PreviewMessage.Text = T("Loading");
        try
        {
            TracePreview($"Browser initialization started: visible={Preview.IsVisible}; size={Preview.ActualWidth}x{Preview.ActualHeight}.");
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductInfo.Name, "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, folder);
            TracePreview("Browser environment created.");
            if (closing) return;
            Preview.ApplyTemplate();
            Preview.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Preview.EnsureCoreWebView2Async(environment);
            if (closing) return;
            Preview.CoreWebView2.Settings.IsScriptEnabled = false;
            Preview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Preview.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Preview.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Preview.CoreWebView2.NavigationStarting += OnNavigation;
            Preview.CoreWebView2.NewWindowRequested += OnNewBrowserWindow;
            Preview.CoreWebView2.PermissionRequested += OnPermissionRequested;
            Preview.CoreWebView2.DownloadStarting += OnDownload;
            Preview.CoreWebView2.NavigationCompleted += OnPreviewNavigationCompleted;
            Preview.CoreWebView2.ProcessFailed += OnPreviewProcessFailed;
            Preview.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webReady = true; PreviewHeading.Text = T("Preview"); ScheduleRefresh();
            TracePreview("Browser initialized.");
        }
        catch (Exception error) { if (!closing) ShowPreviewFailure(T("PreviewUnavailable") + "\n\n" + error.Message); }
        finally { webStarting = false; }
    }

    /// <summary>Maps local images and reloads the sanitized document at its fixed origin.</summary>
    private void NavigatePreview(string body)
    {
        Preview.Visibility = Visibility.Visible;
        if (mappedFolder is not null) Preview.CoreWebView2.ClearVirtualHostNameToFolderMapping("preview.neopad.invalid");
        mappedFolder = document.Path is null ? null : Path.GetDirectoryName(document.Path);
        if (mappedFolder is not null) Preview.CoreWebView2.SetVirtualHostNameToFolderMapping("preview.neopad.invalid", mappedFolder, CoreWebView2HostResourceAccessKind.DenyCors);
        var html = Rendering.Preview(body, Palette).Replace("<head>", "<head><base href='https://preview.neopad.invalid/'>", StringComparison.Ordinal);
        previewNavigationPending = true;
        TracePreview($"Navigating generated HTML, length {html.Length}.");
        Preview.NavigateToString(html);
    }

    /// <summary>Shows actionable errors rather than leaving an empty preview surface.</summary>
    private void ShowPreviewFailure(string message)
    {
        PreviewHeading.Text = T("Preview");
        PreviewMessage.Text = message;
        // Composition hosting keeps this error overlay visible above browser content.
        PreviewNotice.Visibility = Visibility.Visible;
        RetryPreviewButton.Visibility = Visibility.Visible;
    }

    /// <summary>Removes the startup notice only after an actual successful page navigation.</summary>
    private async void OnPreviewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        TracePreview($"Navigation completed: {e.NavigationId}; active={activePreviewNavigation}; success={e.IsSuccess}; status={e.WebErrorStatus}.");
        if (closing || !previewNavigationPending || e.NavigationId != activePreviewNavigation) return;
        if (e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled) return;
        if (!e.IsSuccess) { previewNavigationPending = false; ShowPreviewFailure(T("PreviewFailed") + " " + e.WebErrorStatus); return; }
        try
        {
            await Preview.CoreWebView2.ExecuteScriptAsync(PreviewScripts.SetZoom(zoom));
            if (closing || e.NavigationId != activePreviewNavigation) return;
            previewNavigationPending = false;
            PreviewNotice.Visibility = Visibility.Collapsed;
        }
        catch (Exception error) { if (!closing) ShowPreviewFailure(error.Message); }
    }

    /// <summary>Reports renderer crashes and permits a reload when the browser process survives.</summary>
    private void OnPreviewProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        ShowPreviewFailure(T("PreviewFailed") + " " + e.ProcessFailedKind);
    }

    /// <summary>Retries initialization or reloads the current rendered document.</summary>
    private async void OnRetryPreview(object sender, RoutedEventArgs e)
    {
        Preview.Visibility = Visibility.Visible;
        if (webReady) ScheduleRefresh();
        else await EnsurePreview();
    }

    /// <summary>Applies native caption styling after Windows creates this window's handle.</summary>
    private void OnSourceInitialized(object? sender, EventArgs e) => WindowTheme.ApplyTitleBar(this, Dark);

    /// <summary>Zooms editor text with Ctrl+wheel while leaving normal scrolling untouched.</summary>
    private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || Keyboard.IsKeyDown(Key.RightAlt)) return;
        if (!Editor.IsMouseOver && !PreviewHost.IsMouseOver) return;
        e.Handled = true;
        SetZoom(zoom + Math.Sign(e.Delta) * 0.1);
    }

    /// <summary>Updates the editor and preview scale without changing the configured base font size.</summary>
    private async void SetZoom(double factor)
    {
        var vertical = Editor.VerticalOffset;
        var horizontal = Editor.HorizontalOffset;
        zoom = Math.Clamp(Math.Round(factor, 2), 0.5, 3);
        Editor.FontSize = Math.Clamp(preferences.FontSize, 8, 72) * zoom;
        Editor.UpdateLayout();
        Editor.ScrollToVerticalOffset(vertical);
        Editor.ScrollToHorizontalOffset(horizontal);
        UpdateStatus();
        try
        {
            if (webReady && !previewNavigationPending) await Preview.CoreWebView2.ExecuteScriptAsync(PreviewScripts.SetZoom(zoom));
        }
        catch (Exception error) { if (!closing) ShowPreviewFailure(error.Message); }
    }

    /// <summary>Allows only the generated page inside the preview and sends external links to Windows.</summary>
    private void OnNavigation(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        TracePreview($"Navigation starting: {e.NavigationId}; scheme={new Uri(e.Uri).Scheme}; pending={previewNavigationPending}.");
        // NavigateToString starts a host-generated data:text/html navigation before exposing about:blank.
        if (previewNavigationPending && !e.IsUserInitiated && e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
        {
            activePreviewNavigation = e.NavigationId;
            return;
        }
        if (e.Uri == "about:blank" || e.Uri.StartsWith("about:blank#", StringComparison.Ordinal) || e.Uri.StartsWith("https://preview.neopad.invalid/#", StringComparison.Ordinal))
        {
            if (previewNavigationPending) activePreviewNavigation = e.NavigationId;
            return;
        }
        e.Cancel = true; OpenExternal(e.Uri);
    }

    /// <summary>Intercepts links that request another browser window.</summary>
    private void OnNewBrowserWindow(object? sender, CoreWebView2NewWindowRequestedEventArgs e) { e.Handled = true; OpenExternal(e.Uri); }

    /// <summary>Denies browser permission prompts originating from document content.</summary>
    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e) => e.State = CoreWebView2PermissionState.Deny;

    /// <summary>Prevents document content from initiating downloads inside the editor.</summary>
    private void OnDownload(object? sender, CoreWebView2DownloadStartingEventArgs e) => e.Cancel = true;

    /// <summary>Opens ordinary web and mail links using the user's default applications.</summary>
    private void OpenExternal(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto" && uri.Host != "preview.neopad.invalid")
            Execute(() => Process.Start(new ProcessStartInfo(value) { UseShellExecute = true }));
    }

    /// <summary>Handles application shortcuts while retaining native editor shortcuts.</summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // AltGr is reported as Ctrl+Alt on Windows: leave it entirely to text input.
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || Keyboard.IsKeyDown(Key.RightAlt)) return;
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control); var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        Action? action = control ? e.Key switch
        {
            Key.N => shift ? NewWindow : NewDocument, Key.O => OpenDialog, Key.S => () => Save(shift),
            Key.F when shift => FormatDocument,
            Key.F or Key.H => ShowSearch, Key.G => GoToLine,
            Key.D0 or Key.NumPad0 => () => SetZoom(1),
            Key.OemPlus or Key.Add => () => SetZoom(zoom + 0.1),
            Key.OemMinus or Key.Subtract => () => SetZoom(zoom - 0.1), _ => null
        } : e.Key switch { Key.F3 => () => Find(shift), Key.F5 => InsertDate, Key.Escape => () => SearchPanel.Visibility = Visibility.Collapsed, _ => null };
        if (action is null) return;
        e.Handled = true; Execute(action);
    }

    /// <summary>Deletes the selection or the next character.</summary>
    private void DeleteSelection()
    {
        if (Editor.SelectionLength > 0) Editor.SelectedText = "";
        else if (Editor.CaretOffset < Editor.Document.TextLength) Editor.Document.Remove(Editor.CaretOffset, 1);
    }

    /// <summary>Inserts the localized date and time.</summary>
    private void InsertDate() => Editor.SelectedText = DateTime.Now.ToString("g", System.Globalization.CultureInfo.GetCultureInfo(preferences.Language));

    /// <summary>Displays the search and replacement toolbar.</summary>
    private void ShowSearch() { SearchPanel.Visibility = Visibility.Visible; SearchBox.Focus(); SearchBox.SelectAll(); }

    /// <summary>Finds the next or previous literal match with wraparound.</summary>
    private void Find(bool backwards)
    {
        var query = SearchBox.Text; if (query.Length == 0) { ShowSearch(); return; }
        var text = Editor.Text;
        var start = backwards ? Editor.SelectionStart - 1 : Editor.SelectionStart + Editor.SelectionLength;
        var index = backwards ? start < 0 || text.Length == 0 ? -1 : text.LastIndexOf(query, Math.Min(start, text.Length - 1), StringComparison.OrdinalIgnoreCase)
            : text.IndexOf(query, Math.Min(start, text.Length), StringComparison.OrdinalIgnoreCase);
        if (index < 0) index = backwards ? text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase) : text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) { Inform(T("NotFound")); return; }
        Editor.Select(index, query.Length); Editor.ScrollToLine(Editor.Document.GetLineByOffset(index).LineNumber); Editor.Focus();
    }

    /// <summary>Runs the next-match command from the toolbar.</summary>
    private void OnFind(object sender, RoutedEventArgs e) => Find(false);

    /// <summary>Replaces the current matching selection and finds the next occurrence.</summary>
    private void OnReplace(object sender, RoutedEventArgs e)
    {
        if (SearchBox.Text.Length > 0 && string.Equals(Editor.SelectedText, SearchBox.Text, StringComparison.OrdinalIgnoreCase)) Editor.SelectedText = ReplaceBox.Text;
        Find(false);
    }

    /// <summary>Replaces all literal occurrences in one undoable editor operation.</summary>
    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        if (SearchBox.Text.Length == 0) return;
        Editor.Document.Replace(0, Editor.Document.TextLength, Editor.Text.Replace(SearchBox.Text, ReplaceBox.Text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Closes the search toolbar and restores editor focus.</summary>
    private void OnCloseSearch(object sender, RoutedEventArgs e) { SearchPanel.Visibility = Visibility.Collapsed; Editor.Focus(); }

    /// <summary>Moves the caret to a user-selected existing line.</summary>
    private void GoToLine()
    {
        var input = new TextBox { Text = Editor.TextArea.Caret.Line.ToString(), Margin = new Thickness(12), MinWidth = 180 };
        var dialog = new Window { Owner = this, Title = T("GoTo").Replace("_", ""), SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        WindowTheme.PrepareDialog(dialog, this, Dark);
        var panel = new StackPanel(); panel.Children.Add(input);
        var button = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(12), Padding = new Thickness(12, 4, 12, 4) };
        button.Click += (_, _) => { if (int.TryParse(input.Text, out var line) && line > 0 && line <= Editor.Document.LineCount) { Editor.CaretOffset = Editor.Document.GetLineByNumber(line).Offset; Editor.ScrollToLine(line); dialog.DialogResult = true; } else Inform(T("InvalidLine")); };
        panel.Children.Add(button); dialog.Content = panel; dialog.ShowDialog(); Editor.Focus();
    }

    /// <summary>Toggles persistent line wrapping.</summary>
    private void ToggleWrap() { preferences.WordWrap = !preferences.WordWrap; preferences.Save(); ApplyPreferences(); }

    /// <summary>Shows editable per-user theme, font, size and language preferences.</summary>
    private void ShowSettings()
    {
        var dialog = new Window { Owner = this, Title = T("Settings").Replace("_", ""), Width = 400, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Background, Foreground = Foreground };
        WindowTheme.PrepareDialog(dialog, this, Dark);
        var panel = new StackPanel { Margin = new Thickness(20) };
        var theme = new ComboBox { ItemsSource = AppThemes.Names.Select(T).ToArray(), SelectedIndex = Math.Max(0, AppThemes.Names.ToList().IndexOf(preferences.Theme)) };
        var font = new ComboBox { ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToArray(), SelectedItem = preferences.Font };
        var size = new TextBox { Text = preferences.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        var language = new ComboBox { ItemsSource = new[] { "Polski", "English" }, SelectedIndex = preferences.Language == "pl" ? 0 : 1 };
        foreach (var (key, control) in new (string, Control)[] { ("Theme", theme), ("Font", font), ("Size", size), ("Language", language) })
        { panel.Children.Add(new TextBlock { Text = T(key), Margin = new Thickness(0, 10, 0, 4) }); panel.Children.Add(control); }
        var apply = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(12, 5, 12, 5) };
        apply.Click += (_, _) => Execute(() =>
        {
            preferences.Theme = AppThemes.Names[theme.SelectedIndex]; preferences.Font = font.SelectedItem as string ?? "Consolas";
            if (double.TryParse(size.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)) preferences.FontSize = Math.Clamp(value, 8, 72);
            preferences.Language = language.SelectedIndex == 0 ? "pl" : "en"; preferences.Save(); ApplyPreferences(); UpdateStatus(); dialog.DialogResult = true;
        });
        panel.Children.Add(apply); dialog.Content = panel; dialog.ShowDialog();
    }

    /// <summary>Disables automatic analysis when document size or unusually long lines would make editing expensive.</summary>
    private void UpdateEditingMode(bool force = false)
    {
        var next = Editor.Document.TextLength > 1_000_000 || Editor.Document.LineCount > 10_000 || Editor.Document.Lines.Any(line => line.Length > 20_000);
        if (next != limitedEditing || force)
        {
            limitedEditing = next;
            Editor.SyntaxHighlighting = next ? null : EditorHighlighting.Create(document.Format, Dark);
            if (next) markers.Diagnostics = [];
        }
    }

    /// <summary>Remembers the user's split without changing either column during a theme or font update.</summary>
    private void OnDividerDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (!PreviewVisible) return;
        var total = EditorColumn.ActualWidth + PreviewColumn.ActualWidth;
        if (total > 0) previewShare = Math.Clamp(PreviewColumn.ActualWidth / total, 0.1, 0.9);
    }

    /// <summary>Closes only the preview pane while retaining the selected file format.</summary>
    private void OnClosePreview(object sender, RoutedEventArgs e)
    {
        previewHidden = true; UpdateFormatVisibility(); ScheduleRefresh();
    }

    /// <summary>Restores preview using the saved split when the current format supports it.</summary>
    private void OnOpenPreview(object sender, RoutedEventArgs e)
    {
        previewHidden = false; UpdateFormatVisibility(); ScheduleRefresh();
    }

    /// <summary>Formats supported valid documents as one undoable change without writing to disk.</summary>
    private void FormatDocument()
    {
        if (!CanFormat) return;
        try
        {
            var formatted = FileFormatter.Format(Editor.Text, document.Format);
            if (formatted == Editor.Text) return;
            var caret = Editor.CaretOffset;
            Editor.Document.Replace(0, Editor.Document.TextLength, formatted);
            Editor.CaretOffset = Math.Min(caret, Editor.Document.TextLength);
        }
        catch (Exception error) when (error is JsonException or System.Xml.XmlException)
        { Inform(T("FormattingError") + "\n\n" + error.Message, T("Error")); }
    }

    /// <summary>Shows a themed question, including explicit Save/Discard/Cancel labels for dirty documents.</summary>
    private MessageBoxResult Ask(string message, bool saveChoices = false)
    {
        var actions = saveChoices
            ? new[] { new DialogAction(T("Save").Replace("_", ""), MessageBoxResult.Yes, true), new DialogAction(T("Discard"), MessageBoxResult.No), new DialogAction(T("Cancel"), MessageBoxResult.Cancel, IsCancel: true) }
            : new[] { new DialogAction(T("Yes"), MessageBoxResult.Yes, true), new DialogAction(T("No"), MessageBoxResult.No, IsCancel: true) };
        var dialog = new AppDialog(this, Dark, ProductInfo.Name, message, actions);
        dialog.ShowDialog(); return dialog.Choice;
    }

    /// <summary>Shows an informational or error message using the current application theme.</summary>
    private void Inform(string message, string? title = null)
    {
        new AppDialog(this, Dark, title ?? ProductInfo.Name, message,
            new DialogAction("OK", MessageBoxResult.OK, true, true)).ShowDialog();
    }

    /// <summary>Exercises actual WPF controls and WebView rendering on a Windows CI desktop.</summary>
    internal async void RunSmokeTest(string? reportPath = null)
    {
        reportPath ??= Path.Combine(AppContext.BaseDirectory, "smoke-result.txt");
        var directory = Path.Combine(Path.GetTempPath(), "NeoNoteSmoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            if (!IsLoaded) await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Width = Math.Min(1000, SystemParameters.WorkArea.Width - 40);
            Height = Math.Min(700, SystemParameters.WorkArea.Height - 40);
            Left = 20; Top = 20; Activate();
            VerifyBuiltIconTransparency();
            Editor.Text = "# Smoke heading\n\n**Rendered**\n\n<h2>HTML heading</h2>";
            if (!Title.StartsWith("*")) throw new InvalidOperationException("Dirty title was not updated.");
            document.Save(Path.Combine(directory, "smoke.md")); UpdateStatus();
            if (Title.StartsWith("*")) throw new InvalidOperationException("Saved title remained dirty.");
            SearchBox.Text = "Rendered"; Find(false);
            if (Editor.SelectedText != "Rendered") throw new InvalidOperationException("Search selection failed.");
            ReplaceBox.Text = "Replaced"; OnReplaceAll(this, new RoutedEventArgs());
            if (!Editor.Text.Contains("Replaced")) throw new InvalidOperationException("Replacement failed.");
            Editor.Undo();
            if (!Editor.Text.Contains("Rendered")) throw new InvalidOperationException("Undo failed.");
            var limit = DateTime.UtcNow.AddSeconds(45);
            while (!webReady && DateTime.UtcNow < limit) await Task.Delay(100);
            if (!webReady) throw new InvalidOperationException("WebView2 did not initialize: " + PreviewMessage.Text);
            await VerifyPreviewHeadingAsync("Smoke heading");
            await CapturePreviewArtifactsAsync(reportPath);
            var htmlHeading = await Preview.CoreWebView2.ExecuteScriptAsync("document.querySelector('h2').textContent");
            if (JsonSerializer.Deserialize<string>(htmlHeading) != "HTML heading") throw new InvalidOperationException("Embedded HTML heading did not render.");
            document.Save(document.Path!);
            var markdownPath = document.Path!;
            var jsonPath = Path.Combine(directory, "document.json");
            File.WriteAllText(jsonPath, "{\"example\": true}");
            OpenPath(jsonPath);
            if (PreviewHost.Visibility != Visibility.Collapsed) throw new InvalidOperationException("JSON did not hide preview.");
            OpenPath(markdownPath);
            await VerifyPreviewHeadingAsync("Smoke heading");
            if (PreviewHost.Visibility != Visibility.Visible) throw new InvalidOperationException("Reopening Markdown did not restore preview.");
            await VerifyWheelGesturesAsync();
            SetZoom(1.2);
            if (Math.Abs(Editor.FontSize - preferences.FontSize * 1.2) > 0.01) throw new InvalidOperationException("Editor zoom failed.");
            SetZoom(1);
            await VerifyEditingFeaturesAsync(directory);
            foreach (var theme in new[] { "Light", "Dark" })
            {
                preferences.Theme = theme; ApplyPreferences();
                var menu = (MenuItem)MainMenu.Items[0]; menu.IsSubmenuOpen = true;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var popup = (System.Windows.Controls.Primitives.Popup)menu.Template.FindName("PART_Popup", menu);
                var border = (Border)popup.Child;
                if (((SolidColorBrush)border.Background).Color != ((SolidColorBrush)Resources["Panel"]).Color)
                    throw new InvalidOperationException("Menu popup did not use the selected theme.");
                menu.IsSubmenuOpen = false;
            }
            preferences.Language = "en"; ApplyPreferences();
            if ((string)((MenuItem)MainMenu.Items[0]).Header != "_File") throw new InvalidOperationException("English menu was not applied.");
            document.Save(document.Path!); Editor.Text = document.Text;
            File.WriteAllText(reportPath, "PASSED: document editing, Markdown/HTML rendering, native zoom without scrolling, out-of-window splitter drags and pane minimums, font changes without pane shrinkage, preview hide/reopen, all palettes, themed save choices and scrollbars, undoable JSON formatting, 61 MB file rejection, large-file analysis safeguards, Polish AltGr+S input and localization.");
            Close(); Application.Current.Shutdown(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(reportPath, error + "\n\nPreview lifecycle:\n" + string.Join("\n", previewTrace));
            closing = true; Application.Current.Shutdown(1);
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>Waits for the real debounced preview path, including completion of browser navigation.</summary>
    private async Task VerifyPreviewHeadingAsync(string expected)
    {
        var limit = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < limit)
        {
            if (webReady && !previewNavigationPending && PreviewNotice.Visibility == Visibility.Collapsed)
            {
                var value = await Preview.CoreWebView2.ExecuteScriptAsync("document.querySelector('h1')?.textContent ?? null");
                if (JsonSerializer.Deserialize<string>(value) == expected) return;
            }
            await Task.Delay(100);
        }
        var state = webReady ? await Preview.CoreWebView2.ExecuteScriptAsync("JSON.stringify({url:location.href,body:document.body?.innerText.slice(0,150),width:innerWidth,height:innerHeight})") : "not initialized";
        throw new InvalidOperationException($"Preview did not render the expected heading: {PreviewMessage.Text}. Pending={previewNavigationPending}; notice={PreviewNotice.Visibility}; pane={Preview.ActualWidth}x{Preview.ActualHeight}; browser={state}");
    }

    /// <summary>Verifies real Ctrl+wheel suppresses scrolling and ordinary wheel input still scrolls both panes.</summary>
    private async Task VerifyWheelGesturesAsync()
    {
        Editor.Text = "# Wheel heading\n\n[Example](https://example.com)\n\n" + string.Join("\n\n", Enumerable.Range(1, 100).Select(i => $"Paragraph {i}: sample document content."));
        await VerifyPreviewHeadingAsync("Wheel heading");
        Editor.ScrollToVerticalOffset(200);
        await Preview.CoreWebView2.ExecuteScriptAsync("window.scrollTo(0, 200)");
        await Task.Delay(200);
        var offset = Editor.VerticalOffset;
        var scale = zoom;
        await WindowsInputProbe.WheelAsync(Editor, 120, true);
        if (zoom <= scale || Math.Abs(Editor.VerticalOffset - offset) > 1)
            throw new InvalidOperationException($"Editor Ctrl+wheel scrolled or failed to zoom: {offset} -> {Editor.VerticalOffset}, scale {scale} -> {zoom}.");
        scale = zoom;
        await WindowsInputProbe.WheelAsync(Editor, -120, true);
        if (zoom >= scale || Math.Abs(Editor.VerticalOffset - offset) > 1)
            throw new InvalidOperationException("Editor Ctrl+wheel zoom-out changed the scroll position.");
        var y = double.Parse(await Preview.CoreWebView2.ExecuteScriptAsync("window.scrollY"), System.Globalization.CultureInfo.InvariantCulture);
        if (y < 100) throw new InvalidOperationException("Preview wheel test did not start at a scrolled position.");
        scale = zoom;
        await WindowsInputProbe.WheelAsync(Preview, 120, true);
        var after = double.Parse(await Preview.CoreWebView2.ExecuteScriptAsync("window.scrollY"), System.Globalization.CultureInfo.InvariantCulture);
        if (zoom <= scale || Math.Abs(y - after) > 1)
            throw new InvalidOperationException($"Preview Ctrl+wheel scrolled or failed to zoom: {y} -> {after}, scale {scale} -> {zoom}.");
        scale = zoom;
        await WindowsInputProbe.WheelAsync(Preview, -120, true);
        after = double.Parse(await Preview.CoreWebView2.ExecuteScriptAsync("window.scrollY"), System.Globalization.CultureInfo.InvariantCulture);
        if (zoom >= scale || Math.Abs(y - after) > 1)
            throw new InvalidOperationException("Preview Ctrl+wheel zoom-out changed the scroll position.");
        await WindowsInputProbe.WheelAsync(Preview, -120, false);
        after = double.Parse(await Preview.CoreWebView2.ExecuteScriptAsync("window.scrollY"), System.Globalization.CultureInfo.InvariantCulture);
        if (after <= y) throw new InvalidOperationException("Normal preview wheel input did not scroll.");
        offset = Editor.VerticalOffset;
        await WindowsInputProbe.WheelAsync(Editor, -120, false);
        if (Editor.VerticalOffset <= offset) throw new InvalidOperationException("Normal editor wheel input did not scroll.");
        document.Save(document.Path!);
    }

    /// <summary>Captures both browser pixels and the composed WPF window for visual regression review.</summary>
    private async Task CapturePreviewArtifactsAsync(string reportPath)
    {
        await using (var stream = File.Create(Path.ChangeExtension(reportPath, ".browser.png")))
            await Preview.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        await Task.Delay(200);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.ChangeExtension(reportPath, ".window.png"));
        encoder.Save(output);
    }

    /// <summary>Records bounded lifecycle metadata without document contents for automated test diagnosis.</summary>
    private void TracePreview(string message)
    {
        if (previewTrace.Count == 40) previewTrace.RemoveAt(0);
        previewTrace.Add(message);
    }
}
