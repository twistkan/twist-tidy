# Verification record

Date: 26 October 2026. Target: Windows x64, .NET 8 WPF, version 1.0.0.

## Confirmed

- Release compilation: zero warnings and zero errors.
- **38 safety and service checks passed, zero failures.** Test code is in `tests/TwistTidy.Tests/Program.cs`.
- Tests cover system-folder and drive-root protection, traversal rejection, reserved names, numeric suffixes, scan exclusions and cancellation, media planning without mutation, unknown-extension skipping, project nesting and custom naming, unchanged previews, destination clashes, persistent move/undo round trips, edited-file protection, nonempty folder protection, content-based duplicates, extension/size filtering, full-hash rejection of a deliberate partial-hash collision, changed keeper rejection, locked files, cancelled execution, pending journal recovery, custom settings, malformed JSON preservation, interrupted undo recovery, and duplicate review-folder move/undo.
- Native Shell recycling and restoration were exercised against disposable files on this PC. File contents were checked after restoration.
- Nested project creation with a README was exercised and undone. The README was recycled and the created empty folder tree was removed.
- WPF visual renders were produced for Home, Sorter, Duplicates, Builder, History, Settings, About, Templates, Setup and the Light theme. Inspected renders are under `docs/screenshots`. Contrast and the visibility of primary scan/preview actions were corrected based on these images.
- No real media library was scanned or reorganized. Test fixtures are isolated under `.test-data`.
- Source audit found no application networking APIs, browser controls, external asset URLs, unfinished implementation markers or em dashes in UI strings. XAML namespace URLs are XML identifiers and do not fetch content.
- Source projects have no third-party package references. Microsoft runtime redistribution notices are included in the package and embedded in the setup.
- The final standalone executable's setup-copy and native shortcut smoke check passed. The copied executable's complete hash matched the source, and the saved shortcut was reloaded and its target verified in `.test-data/package-check`. No system shortcuts were created by this check.

The package copy/shortcut smoke check is reproducible with:

```powershell
.\dist\TwistTidy\TwistTidy.exe --package-check "$PWD\.test-data\package-check"
```

It uses the actual setup file-copy routine, compares complete source/destination SHA-256 hashes, creates and reloads a native Windows Shell shortcut, verifies its target, and retains the fixture and `package-check.txt` for inspection. It installs only into the supplied test directory and creates no desktop or Start menu shortcuts.

## Verification limits

- The computer-use capture helper repeatedly returned `window capture timed out: timed out waiting on channel` (the initial attempt also reported `FrameArrived timed out`). Interactive UI click-through automation could not be completed. The image checks are real WPF renders, not captured interactive screenshots or proof of a complete UI end-to-end test.
- Visual QA was performed at the default window size on this PC. Other scaling factors, accessibility tools and multiple monitors have not been independently tested.
- The app is published with its runtime for Windows x64. There has been no separate clean-PC installation test, and the executable is unsigned.
- There is no full packet capture or firewall-blocked test in this verification record. The offline design is supported by source inspection and lack of application network calls, not a claim that all third-party Windows metadata handlers were audited.
- A proprietary codec, metadata handler, exceptional path length or inaccessible directory can fail on another PC. Metadata fallbacks and per-file error reporting remain available.
- Recycle Bin storage and cleanup are controlled by Windows. Restoration cannot be promised after an item has left the bin. The app deliberately protects edited files and skips uncertain cases rather than forcing an undo.
- Journal persistence reduces interruption risk but cannot make a multi-file batch atomic or protect against drive failure. Pending directory creation is conservatively left in place if ownership cannot be proved after interruption.

See the root README for the operating guide, source architecture and beginner build/publish steps.
