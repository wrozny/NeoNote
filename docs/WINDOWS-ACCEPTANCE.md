# Windows 10 acceptance checklist

Target: Windows 10 22H2 x64 with current Edge WebView2 Runtime. A Linux cross-build does not establish these results. Record OS, runtime versions, package checksum, and results before promoting a prerelease.

- Install per user without administrator rights; find MarkdownPreview through Start search. Uninstall and confirm the Start shortcut and application registration disappear while documents remain intact.
- Launch a blank window. Confirm `*Bez tytułu - MarkdownPreview`, a responsive editor, and Markdown preview.
- Check Ctrl+wheel in both panes, Ctrl+plus/minus, Ctrl+0 and zoom bounds (50–300%); confirm ordinary wheel input still scrolls.
- Check the native title bar and every nested menu and settings dropdown in both light and dark themes. Test theme changes while the application is open.
- Open Markdown, then JSON, then Markdown again; confirm the preview is visible and updated. Test cold startup with a command-line Markdown path and restore after a failed navigation.
- Type headings, lists, task lists, tables, fenced code, HTML headings, details, links and local/remote images. Confirm rendering, relative image loading after Save As, and no script execution.
- Save a scratch document; cancel Save As and verify text and dirty state survive. Save to a read-only location and verify a visible error without data loss.
- Modify a saved document; test Save, Save As and subsequent Save. Compare original and new files.
- Exercise all three unsaved-change choices for New, Open, Close and Alt+F4, including cancelling the save picker after choosing Save.
- Open separate windows and verify independent text, histories and paths. Close one window while retaining the other.
- Open JSON, XML, INI and TOML files, introduce malformed syntax, and check locations and underlines. Switch formats manually and verify preview visibility.
- Exercise Undo/Redo, cut/copy/paste/delete, literal find in both directions, replace/all, Go to Line, select all and F5. Confirm Ctrl+S and Ctrl+Shift+S.
- Change theme, font, size, language and wrapping; restart and confirm persistence. Inspect menu popup contrast and keyboard access.
- Open UTF-8 with/without BOM and UTF-16 LE/BE files with LF/CRLF; edit, save and compare bytes. Legacy fallback is Windows-1250, not arbitrary encoding detection.
- Edit/delete a file externally; confirm reload prompts and overwrite protection. Test declining a reload followed by saving.
- Launch by command-line path, Explorer Open With and drag/drop. Test Unicode paths and spaces.
- Test without WebView2: text editing and saving must work, with an explanatory preview message.
- Measure cold/warm startup, typing with a large document and memory after repeated document changes. Record actual values rather than assuming performance targets passed.

Known preview-stage limitations: diagnostics are parser messages (which may be English), and underline entire error lines; INI uses a simple section/key=value dialect; no session recovery, installer signing, encoding chooser, Markdown scroll synchronization or arbitrary embedded scripts. Fenced code colors are available for languages recognized by ColorCode; unknown languages remain plain code. The system theme is read when preferences are applied.
