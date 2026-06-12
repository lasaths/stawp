# stawp

Grasshopper solver kill switch. Adds a **Solver** toggle to the canvas toolbar (below the component ribbon) and global shortcut **`Ctrl+Shift+K`** that works even when the GH UI is frozen.

Uses **K** (kill) — avoids Rhino defaults (`Ctrl+Shift+L` = Unlock Selected, `Ctrl+Alt+L` = Unlock).

Windows only (Rhino 7 + 8).

## Install

1. Build (see below) or download `stawp.gha`
2. Right-click the file → **Properties** → **Unblock** (Windows)
3. Copy to Grasshopper Libraries:
   - `%AppData%\Grasshopper\Libraries`
   - Or Grasshopper → File → Special Folders → Components Folder
4. Restart Rhino and open Grasshopper

| Rhino version | Build output |
|---------------|----------------|
| **Rhino 7** | `bin/Release/net48/stawp.gha` |
| **Rhino 8** (.NET Core, default) | `bin/Release/net8.0-windows/stawp.gha` |
| **Rhino 8** (.NET Framework `/netfx`) | `bin/Release/net48/stawp.gha` |

## Usage

| Control | Action |
|---------|--------|
| **Solver** toolbar button | Toggle solver on/off — Phosphor `lock-open` (green) / `lock` (grey) |
| **`Ctrl+Shift+K`** (idle) | Lock solver, or unlock when idle-locked |
| **`Ctrl+Shift+K`** (while solving) | **Panic stop** — reinforces lock + abort; never unlocks mid-solve |

While a solve is running (or a stop campaign is active), **`Ctrl+Shift+K` always reinforces the stop** — no debounce, mash-safe. Unlock only after the solve has finished and the solver is idle-locked.

When stopping, **stawp keeps retrying** `RequestAbortSolution()` every ~20 ms until the solution ends. A small overlay (bottom-right) shows retry count, elapsed time, active components, and a progress bar.

Shortcuts use Win32 `RegisterHotKey` on a dedicated thread. Fallback: `GetAsyncKeyState` poller (~8 ms).

## Build

Uses the official **[Rhino.Templates](https://www.nuget.org/packages/Rhino.Templates)** Grasshopper project layout (same as `dotnet new grasshopper`).

### One-time: install templates

```powershell
dotnet new install Rhino.Templates
```

### Build both targets

```powershell
dotnet build stawp.sln -c Release
```

### Visual Studio

1. Open `stawp.sln`
2. Set startup profile in **Properties → Debug**:
   - **Rhino 7** — debug `net48` against Rhino 7
   - **Rhino 8 - netcore** — debug `net8.0-windows` (Rhino 8 default runtime)
   - **Rhino 8 - netfx** — debug `net48` against Rhino 8 `/netfx`
3. F5 — Rhino launches with the built `.gha` on the package path

Requires Rhino 7 and/or 8 with Grasshopper installed.

### Yak packages (Package Manager)

Release build auto-stages `.yak` files when Rhino 8’s `Yak.exe` is present (`C:\Program Files\Rhino 8\System\Yak.exe`).

```powershell
dotnet build stawp.sln -c Release
```

Outputs in `dist/`:

| File | Rhino |
|------|-------|
| `stawp-*-rh8_*-any.yak` | Rhino 8 (`net48` + `net8.0-windows`) |
| `stawp-*-rh7_*-any.yak` | Rhino 7 (`net48` only) |

Publish to your Yak account (one-time `yak login`):

```powershell
& "C:\Program Files\Rhino 8\System\Yak.exe" push dist\stawp-*-rh8_*-any.yak
& "C:\Program Files\Rhino 8\System\Yak.exe" push dist\stawp-*-rh7_*-any.yak
```

Users install from Rhino: **Package Manager** → search **stawp** → Install.

Skip yak build: `dotnet build -p:BuildYakPackage=False`

## Limitations

- **Windows only** — hotkeys use Win32 APIs
- **Cooperative abort (GH1)** — `RequestAbortSolution()` only takes effect after the current component finishes, or sooner if that component implements early abort. Native Rhino booleans, meshes, and similar ops cannot be interrupted mid-command — same ceiling as Escape.
- **GH1 UI thread** — solver runs on the GH UI thread; stawp works around blocked UI via background hotkeys and a separate feedback thread, but cannot kill a native op already in progress.
- **Toolbar click** — emergency path is `Ctrl+Shift+K` when canvas is frozen

## Manual test checklist

1. **Mash test** — heavy solve running, press `Ctrl+Shift+K` rapidly → solver stays locked, abort count climbs, never unlocks mid-solve.
2. **Unlock test** — after solve stops, single `Ctrl+Shift+K` → unlocks.
3. **Native geom** — boolean/mesh op → overlay shows checkpoint wait; shortcut still reinforces abort.
4. **Toolbar** — lock via button during solve; shortcut mash still reinforces (no unlock).
