# Testing on your own Windows computer

A shared folder provides files; it does not give this cloud session a Windows desktop or a process runner. Do not expose SMB or Remote Desktop to the Internet for this purpose.

## Local Codex session

Run Codex CLI in PowerShell in your local clone of this repository. A local session can build and run Windows tools within its configured permissions. Install .NET 10 SDK and Edge WebView2 Runtime first. The cloud chat does not automatically gain access to that session. Terminal access also does not imply visual desktop-control capabilities.

## Diagnostic test without a development environment

Extract a new portable package, then run the included script:

```powershell
powershell -File .\diagnose-windows.ps1 -Executable .\MarkdownPreview.exe
```

The script runs the actual WPF application with generated Markdown and JSON files. It verifies initial preview rendering, reopening Markdown after JSON, HTML, search/replace, undo, zoom and light/dark menu surfaces. Native Windows input also checks that Ctrl+wheel zooms without scrolling and plain wheel input still scrolls in both panes. It writes `platform.txt`, `smoke-result.txt` and browser/window PNG screenshots under `%TEMP%\MarkdownPreview-Diagnostics-*`. No report is uploaded. The test does not open your own documents or save changed editor preferences. Close other application windows first to avoid confusing multiple visible windows during the test.

Share these two report files if a test fails. Include a screenshot separately when reporting appearance problems. Reports may contain local paths from exception messages; review them before sharing.

## Continuous integration

The GitHub Windows runner runs the same test before publishing release binaries. It is not a substitute for checking Windows 10 itself, since hosted runners use a newer Windows release. A self-hosted Windows 10 runner is an optional future route; it requires explicit setup and access scoping on your computer.
