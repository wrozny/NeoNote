# ModernNotepad / NeoNote engineering review

Scope: source inspection and a small isolated XML reproduction. This naming refactor intentionally leaves application behavior, the file-size limit, file formats and release version unchanged. Recommendations below are future work, not implemented features. Large-file performance has not been benchmarked on a Windows target machine in this task.

## Naming boundary

- `ModernNotepad`: application project, C# namespaces and XAML classes.
- `ModernNotepad.Core`: platform-independent document, rendering and parsing library.
- `ModernNotepad.Tests`: core tests.
- `NeoNote`: product name, public EXE, installer, icons, current settings directory and Windows registrations.
- `MarkdownPreview` / `NeoPad` preference directory strings are historical compatibility identifiers. Renaming them would prevent migration of existing settings.
- Markdown preview remains a functional term. The existing virtual-host URI is retained as a runtime identifier; changing protocols is outside a source-only rename.

The cloud checkout directory may retain its original mount name; that is separate from source project names.

## Prioritized findings

| Priority | Finding and evidence | Recommended change |
| --- | --- | --- |
| P1 | `FileFormatter.Format` loads XML with `IgnoreWhitespace = true`. A whitespace-only node between inline elements can be lost. Input `<p>hello <b>a</b> <i>b</i>!</p>` becomes `<p>hello <b>a</b><i>b</i>!</p>`. | Add mixed-content preservation regressions before changing the formatter; preserve significant text nodes and indent only safe element-only regions. Do not treat every whitespace-only node as ignorable without a schema. |
| P1 | `Document.Open` rejects files above 20 MiB. Accepted files are fully read into `byte[]`, decoded into a complete UTF-16 string and retained alongside saved text and the editor's own storage. | Implement a separately designed large-file backend. Keep the current guard until measurable memory/latency targets are met. |
| P1 | `MainWindow.OnTextChanged` reads `Editor.Text` for each edit; `Document.IsDirty` may compare whole strings; `Document.LineEnding` scans text, including during caret/status updates. | Make the editor buffer authoritative. Track revisions/savepoints and newline metadata incrementally. Avoid materializing a full snapshot for routine edits or cursor movement. |
| P1 | `OpenPath`, `Save` and `OnActivated` do synchronous disk operations on the UI thread. Same-sized external-file checks read the entire file. | Use cancellable background I/O, progress and version checks. Use cheap metadata as a first pass and streaming hashes when necessary; specify behavior for concurrent external writes. |
| P1 | Save allocates encoded bytes and another concatenated preamble array before synchronous flushing. External-change detection and final replacement are separate operations. | Stream encoding into the temporary file, then replace atomically. Add failure recovery and recheck the expected disk version near commit; do not promise that atomic replacement alone prevents concurrent overwrites. |
| P2 | `OnRefreshTick` serializes parsing with a semaphore but cannot cancel work already running. JSON/XML/TOML parsing and Markdown sanitization rebuild complete structures. | Debounce by document revision, cancel stale work where supported and use bounded/incremental validation. Long-running noncancellable parsers need explicit resource policies. |
| P2 | `NavigatePreview` reloads the full generated page and remaps the folder on every refresh. `NavigateToString` has a 2 MiB UTF-8 HTML limit, independent of source length. | Preserve preview scroll and update a stable page incrementally. Bound generated HTML and document expansion. Add local-image and navigation regressions before changing the hosting protocol. |
| P2 | `MainWindow.xaml.cs` is about 800 lines and coordinates storage, parsing, menus, settings, browser lifetime and test execution. | Extract document/session commands, storage and preview lifecycle into small services/view models. Keep behavior-focused tests around save/cancel before moving responsibilities. |
| P2 | `FeatureSmokeTests`, native input probes and icon checks are compiled into the shipping WPF app. | Move UI automation into a test harness or test build, retaining realistic packaged-binary smoke coverage. |
| P2 | WebView initialization uses flags and async-void callers; a concurrent caller can return before initialization completes. Process failure reports an error but cannot always rebuild a dead controller. | Share a single initialization task, define readiness/error states, cancellation and bounded recovery, and test close/reopen/process-failure races. |
| P2 | Preferences are independently loaded in each window. Saves are atomic but last-writer-wins; changes are not broadcast to other windows. | Introduce a shared settings service and validated configuration, with explicitly chosen global versus per-window settings. |
| P2 | Encoding falls back to Windows-1250; the status reports a single line-ending label even for mixed endings. | Provide explicit encoding selection, decode-error reporting and mixed-line-ending handling without silent data conversion. |
| P2 | The release workflow tests a Windows-built EXE, then builds another EXE on Linux for publication. Checksums verify upload integrity, not equality with the tested executable. | Publish the exact tested Windows artifact and include provenance. Keep publication separate from ordinary source pushes. |
| P2 | No dependency lockfiles are tracked; transitive dependency and SDK roll-forward updates can change builds. Windows acceptance remains distinct from newer hosted Windows runners. | Add a reproducible toolchain/dependency policy, dependency auditing and a real Windows 10 acceptance target where required. |
| P2 | The repository now has the user's MIT `LICENSE`, but `scripts/package.ps1` explicitly copies only README and third-party notices into application folders. The notices mostly link to upstream licenses. | Include the project license in distributions and audit required notices/license texts for bundled dependencies, rather than relying only on links. |
| P3 | Syntax highlighting is mainly regular-expression based. INI uses a chosen simple dialect; JSON/XML validation does not include schemas. | Describe these contracts accurately; add parser/tokenizer-based highlighting and optional schema support based on actual user needs. |

The preview already sanitizes HTML, disables document scripts, uses a CSP and restricts external link schemes. Preserve these protections. Remote images can still cause network requests; an offline/privacy switch would be useful for untrusted documents.

## A realistic large-file design

Increasing `MaximumFileBytes` is not a large-file implementation. Neither background loading alone nor a memory-mapped file solves the problem if the UI immediately converts everything back into one giant string.

### Phase 1: immediate responsiveness improvements

1. Track edits using revisions and savepoints rather than full saved-string comparisons.
2. Cache newline/encoding metadata and avoid full-text scans on caret changes.
3. Move loading, external-version checks and saving off the UI thread; expose progress and cancellation.
4. Stream encoding and temporary-file writes. Bound undo history, parser work, link detection and preview generation independently.
5. Benchmark minified single-line JSON as well as ordinary multiline text. Rendering a huge individual line needs its own limit or horizontal segmentation.

### Phase 2: bounded-memory viewing

Start with a read-only large-file mode. Open a stream or memory-mapped byte source; build a sparse/incremental line index; decode only visible windows with a bounded cache. Account for UTF-8 multibyte boundaries, UTF-16/32 code units, BOMs, CRLF across chunk boundaries and extremely long lines.

Scrolling and search must work without a full decoded copy. Use streaming search with overlap between chunks; report progress and support cancellation. Avoid continuously parsing or rendering the complete document. Start with plain text; optional visible-range tokenization can follow.

AvalonEdit visual virtualization does not by itself provide an out-of-core document model. Evaluate whether its storage can support the required contract before committing to a custom backend. A dedicated viewer or a proven editor component with demonstrated large-file behavior may be the lower-risk option; measure it on the target OS.

### Phase 3: large-file editing

Use a piece table/tree or comparable buffer that references the immutable original file plus an append-only edit store. Handle bounded undo, search across pieces and a streaming atomic save. Define how external modifications invalidate the original byte source. Integrating such a buffer into the editor is substantial work, not a change to one constant.

### Acceptance measurements

Define supported ranges and test them: 61 MB regression, 100 MB and 1 GB representative files, with larger files as a separately measured target. Include single-line JSON, millions of short lines, Unicode, malformed input, network storage and externally modified files. Record time to first visible text, peak memory, scroll/input latency, cancellation and save/reopen integrity on specified hardware. Do not promise arbitrary file sizes or full live Markdown preview/formatting for multi-gigabyte files.

## Useful feature backlog

Recommended order:

1. Crash recovery and local draft autosave, with a clear retention policy.
2. Read-only large-file viewer, streaming search and tail/follow-log mode.
3. Encoding and newline selectors, recent files and detection of external edits with diff/reload choices.
4. Search options: case sensitivity, whole words, regex and replace preview.
5. Synchronized editor/Markdown scrolling, preview scroll preservation and optional offline images.
6. Optional JSON Schema/XML schema diagnostics and a structured JSON tree view.
7. Portable settings mode, signed installer/update delivery and export of user preferences.
8. Cross-platform UI only as an explicit product goal: WPF is Windows-specific, while the core library is portable. Extract the UI boundary before evaluating Avalonia or other frontends.

## Suggested implementation order

Fix XML content preservation and add recovery tests first. Then remove avoidable full-document copies and UI-thread I/O. Prototype and benchmark bounded-memory read-only viewing before raising file limits. Add large-file editing only after the buffer and save model are proven. Keep branding refactors, behavioral fixes and performance changes in separately reviewable commits.
