# TwisTidy: Build Prompt

## 1. Role

You are a senior Windows desktop developer and UI designer. Build a complete, working, offline desktop app called **TwisTidy**. Deliver full source code, not snippets. After the code, give plain-language steps to build and run it, because I am not a software developer.

## 2. What the app is

TwistTidy is a safe-first file organizer for video production work. It has three hero features:

1. **Footage Sorter**
2. **Duplicate Finder**
3. **Project Folder Builder**

Core rule for the whole app: **Preview, then Approve, then Undo.** Nothing is moved, renamed, or deleted until I approve a preview. Every action can be undone.

## 3. Hard requirements

- **Fully offline.** No internet access of any kind. No telemetry, no analytics, no update checks, no web calls, no online fonts, icons, or images. Do not reference `System.Net.Http`, `HttpClient`, `WebClient`, sockets, or any NuGet package that phones home. Bundle all fonts and assets inside the app.
- **Native Windows UI.** Use **C# with .NET 8 and WPF**. No Electron, no web view, no browser-based UI.
- **Fancy, modern design.** See section 6.
- **Branding.** Show **"Powered by Tweest"** in the UI footer on every screen, in small, tasteful text. Also show it on the About dialog and the splash screen.
- **Safe by default.**
  - Never permanently delete. Send removed files to the Recycle Bin.
  - Never overwrite. On a name clash, add a numeric suffix.
  - Never touch Windows, Program Files, or system folders. Block them.
  - Handle locked files, missing permissions, and very long paths without crashing.
- **Performance.** Scanning must run on background threads. The UI must never freeze. Show progress and a Cancel button.
- **Local only storage.** Save settings, templates, and history as JSON in `%AppData%\TwistTidy`.

## 4. Features

### 4.1 Footage Sorter

Scan a chosen folder (with subfolders) and build a sorting plan.

- Detect media by extension: video (mp4, mov, mxf, avi, mkv, mts, braw, r3d), photo (jpg, jpeg, png, raw, cr2, cr3, arw, dng, heic), audio (wav, mp3, aac, flac), project files (prproj, drp, aep, psd).
- Group by: **Type**, **Shoot date**, **Camera or source**, **Resolution** (when available).
- Camera or source detection by file name pattern, for example `DJI_` = Drone, `GOPR` or `GX` = GoPro, `MVI_` = Canon, `C0001` = Sony, `PXL_` or `IMG_` = Phone. Let me edit these patterns in a Settings screen.
- Shoot date from media metadata when readable, otherwise the file's modified date. Say which one was used.
- Offer sorting presets, for example `Footage\2026-10-26\Drone\` or `Footage\Camera A\2026-10-26\`. Let me create and save my own preset with a simple pattern builder.
- Resolution and duration: read through an **offline, bundled** library or tool only. If unavailable, skip gracefully and say so.

### 4.2 Duplicate Finder

- Find duplicates by **content**, not name. Use three passes for speed: file size, then a quick partial hash, then a full SHA-256 on the remaining candidates.
- Show duplicate groups. In each group, auto-suggest one file to keep (oldest, or the one in the best-organized folder) and let me change it.
- Show **total space recoverable** prominently, as a big number.
- Actions: send selected to Recycle Bin, or move to a "Duplicates" review folder.
- Filters: minimum file size, file type, folders to exclude.
- Include a preview pane for images and basic file info for videos.

### 4.3 Project Folder Builder

- Create a standard folder structure for a new video job in one click.
- Fields: Client name, Project name, Date, Project type.
- Built-in templates: **Video Production** (01_Footage, 02_Audio, 03_Graphics, 04_Edit, 05_Exports, 06_Docs), **Drone Shoot**, **Documentary**, **Commercial**.
- Folder name pattern, for example `2026-10-26_ClientName_ProjectName`.
- Template editor: add, rename, reorder, and nest folders visually. Save and load templates as JSON.
- Optional: drop a small `README.txt` with project details into the new folder.
- Optional: after creation, offer to run the Footage Sorter straight into the new project's Footage folder.

### 4.4 Safety and history (applies to all features)

- **Preview screen** for every operation. A table with columns: File, From, To, Action. I can untick any row.
- **Dry-run summary** before approval: number of files, total size, folders to be created.
- **Undo.** Keep a history log of every batch. One click reverts a whole batch. Moves are reversed, Recycle Bin items are restored where possible.
- **History screen** listing past batches with date, action, and file count.

## 5. Screens

1. **Splash screen** with logo and "Powered by Tweest".
2. **Home dashboard** with three large cards: Footage Sorter, Duplicate Finder, Project Builder. Show quick stats such as last run and space recovered.
3. **Each feature screen**: choose folder, scan, preview, approve.
4. **History and Undo.**
5. **Settings**: camera patterns, sort presets, excluded folders, theme (Dark or Light), accent color.
6. **About**: logo, version, "Powered by Tweest", and a plain statement that the app never connects to the internet.

Layout: left navigation rail with icons, main content area, slim footer with "Powered by Tweest" and an **Offline** badge.

## 6. Design direction

Cinematic and premium, like a color-grading suite crossed with a modern Windows 11 app.

- **Default theme:** dark. Deep charcoal background (`#0F1115`), card surfaces slightly lighter (`#171A21`).
- **Accent:** a gradient from electric violet (`#7C3AED`) to warm orange (`#FB923C`). Use it on buttons, progress bars, active nav items, and the logo.
- **Shape:** rounded corners (12 to 16 px), soft shadows, subtle glow on hover.
- **Typography:** bundle a clean modern font (for example Inter or Segoe UI Variable fallback). Large clear headings, readable body text.
- **Motion:** smooth fades and slides between screens, animated progress, hover and press effects. Keep animations fast and tasteful.
- **Icons:** vector icons drawn in XAML, no online icon fonts.
- **Feel:** spacious, uncluttered, big touch-friendly buttons.
- Optional: Windows 11 Mica or acrylic backdrop if supported, with a solid fallback.

## 7. Name and logo

- **App name:** **TwistTidy** (a nod to my name, Twist).
- **Logo concept:** a rounded-square app icon with a folder shape whose front edge **twists into a ribbon spiral**, so the folder looks like it is being gently wrung into order. A small **film-frame notch** (sprocket hole) sits on the folder tab as a nod to motion pictures. Fill with the violet-to-orange gradient on a dark background.
- Draw the logo as **vector XAML** (`Path` and `Geometry`) so it stays sharp at any size and needs no image files.
- Also generate a multi-size `.ico` file from the logo for the Windows executable and taskbar.
- Wordmark: "TwistTidy" in bold, with "Tidy" in the gradient. Small tagline under it: *Order, twisted your way.*

## 8. Code structure

- MVVM pattern. Folders: `Models`, `ViewModels`, `Views`, `Services`, `Assets`, `Themes`.
- Separate services for scanning, hashing, planning, executing, and undo, so each can be tested alone.
- Clear comments on anything non-obvious. Short and plain.
- Include a few unit tests for the planning logic and undo (the safety-critical parts).

## 9. Delivery format

1. A short summary of the architecture.
2. The **full file tree**.
3. **Every file's complete code**, in order, ready to paste.
4. **Beginner steps** to build and run: which free tool to install (Visual Studio Community or the .NET 8 SDK), how to open the project, and how to press run.
5. How to publish a single-file `.exe` I can use without Visual Studio.
6. A short **offline verification note**: how I can confirm the app makes no network calls (for example, by blocking it in Windows Firewall and checking it still works fully).

## 10. Build order

Build in this order, and make each stage run before moving on:

1. Project setup, theme, navigation, splash, footer branding.
2. Preview, approve, and undo engine (the foundation).
3. Footage Sorter.
4. Project Folder Builder.
5. Duplicate Finder.
6. History, Settings, About.
7. Polish: animations, error handling, edge cases.

## 11. Do not

- Do not add any online features, accounts, or cloud sync.
- Do not add features I did not ask for.
- Do not permanently delete files.
- Do not leave placeholder code or "TODO" in the delivered result.
- Do not use em dashes in any text shown in the UI.
