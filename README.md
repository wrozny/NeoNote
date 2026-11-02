# MarkdownPreview

A compact Windows text editor inspired by Windows 10 Notepad, with a live Markdown/HTML preview and format-aware editing. The default interface is Polish and dark; English and a light theme are available in Settings.

## Run on Windows

Target: Windows 10 22H2 x64 or newer. Install the Microsoft Edge WebView2 Evergreen Runtime for Markdown preview. Portable self-contained builds include .NET; framework-dependent development builds require .NET 10 Desktop Runtime.

Download the `setup.exe` installer or the directly runnable `portable.exe` from the release assets. GitHub's automatically generated source archives are not application packages. The single portable EXE includes .NET and extracts its bundled files on first launch; the installed and extracted-folder variants avoid that extraction step.

Open `MarkdownPreview.exe` or pass a filename as its first argument. The per-user installer registers a Start menu shortcut and an Explorer Open With entry without changing the default Markdown application. Ctrl+wheel and Ctrl+plus/minus zoom the editor and preview; Ctrl+0 resets to 100%.

## Development

Install .NET 10 SDK. Use the existing checkout; cloud tasks are already isolated and do not require a Git worktree.

```sh
dotnet build src/MarkdownPreview/MarkdownPreview.csproj -c Release
dotnet test tests/MarkdownPreview.Tests/MarkdownPreview.Tests.csproj -c Release
```

On Windows:

```powershell
dotnet run --project src/MarkdownPreview -- "C:\Documents\notes.md"
./scripts/package.ps1
```

Packaging additionally requires Inno Setup 6. Output: `artifacts/`, with a self-contained portable ZIP, a smaller `-framework.zip` requiring .NET 10 Desktop Runtime, a self-contained per-user installer, a directly runnable portable EXE and SHA-256 checksums. WebView2 remains a shared prerequisite. The GitHub workflow builds Windows packages on pushes; `v*` tags publish a prerelease after successful build/tests and real WPF/WebView2 smoke runs for both folder and single-file distributions.

For testing on your own computer, see [local Windows testing](docs/LOCAL-TESTING.md). Each portable ZIP includes `diagnose-windows.ps1`.

Linux can run core tests and cross-compile the Windows executable, but cannot run WPF. Complete [Windows acceptance checks](docs/WINDOWS-ACCEPTANCE.md) on the target OS before treating a preview as production-ready.

## Behavior

One document per independent window. Scratch documents default to Markdown. Save As changes document identity. Unsaved changes are confirmed before destructive document operations. Saves preserve recognized encoding and line endings, use a sibling temporary file, and check external modifications before overwriting.

Markdown supports CommonMark plus Markdig advanced extensions, including tables, tasks, fenced code, footnotes and safe HTML. Active HTML is stripped and preview scripts are disabled. Relative images resolve from the saved file's directory. JSON, XML, INI and TOML select syntax coloring and validation instead of preview. Manual format changes do not convert text or rename files.

INI accepts comments beginning with `;` or `#`, section headers and `key=value` entries. XML DTDs are disabled. Non-UTF text without a BOM falls back to Windows-1250; arbitrary legacy encoding detection is intentionally not claimed.

## Structure

- `src/MarkdownPreview.Core`: UI-independent document, encoding, parsing and rendering services.
- `src/MarkdownPreview`: WPF views, window command coordinator, highlighting, translations and user preferences.
- `tests/MarkdownPreview.Tests`: real filesystem round trips, malformed/valid parser cases and rendered HTML checks.
- `packaging` / `scripts`: reproducible Windows distribution.

The initial UI uses a window coordinator rather than a full MVVM framework. Core services are isolated so commands can move into view models as the UI grows. See the acceptance document for current limitations. Dependencies and upstream license references are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
