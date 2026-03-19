# KMux

A C# WPF-based multi-window terminal multiplexer for Windows, optimized for [Claude Code](https://claude.ai/code) workflows. Features tmux-style keybindings, multi-tab/pane splitting, macro recording/playback, and session management.

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation & Build](#installation--build)
- [Quick Start](#quick-start)
- [Keybindings](#keybindings)
- [Claude Code Integration](#claude-code-integration)
- [Tab Management](#tab-management)
- [Pane Splitting](#pane-splitting)
- [Macro System](#macro-system)
- [Session Management](#session-management)
- [Themes & Font Settings](#themes--font-settings)
- [Recent Folders](#recent-folders)
- [Shell Profiles](#shell-profiles)
- [Project Structure](#project-structure)
- [Architecture](#architecture)
- [Configuration File Locations](#configuration-file-locations)

---

## Features

| Feature | Description |
|---------|-------------|
| **Multi-Tab** | Organize work contexts into separate tabs |
| **Pane Splitting** | Binary tree-based horizontal/vertical pane splitting |
| **Claude Code Integration** | Real-time Claude Code activity status in pane headers |
| **Macro Recording/Playback** | Record terminal interactions and replay with timing |
| **Session Management** | Save and restore multi-window/tab/pane layouts |
| **Theme System** | 6 built-in color themes |
| **Live CWD Tracking** | Real-time working directory display via OSC 7 and PEB reading |
| **Recent Folders** | Quick access to frequently used directories |
| **xterm.js Rendering** | Full ANSI/VT100 support via WebView2 |
| **tmux-style Shortcuts** | Ctrl+B prefix with 40+ commands |

---

## Requirements

- **OS:** Windows 10/11 (x64)
- **Runtime:** .NET 8.0
- **WebView2:** Microsoft Edge WebView2 Runtime — [Download here](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- **Build Tools:** Visual Studio 2022 or .NET SDK 8.0

---

## Installation & Build

### Build from Source

```bash
git clone https://github.com/gwkang/kmux.git
cd kmux
dotnet build KMux.sln -c Release
```

Or open `KMux.sln` in Visual Studio 2022 and build.

### Run without Building

```bash
dotnet run --project src/KMux.App/KMux.App.csproj
```

### Build Script

```bash
build.bat
```

Output is placed in `src/KMux.App/bin/x64/Release/net8.0-windows/`.

---

## Quick Start

1. Launch `KMux.exe`.
2. Available shells (PowerShell, WSL, Git Bash, Claude Code, etc.) are auto-detected.
3. A terminal window opens with the default shell.

### Basic Usage

```
Ctrl+B  →  \        # Split pane left/right
Ctrl+B  →  -        # Split pane top/bottom
Ctrl+B  →  Arrow    # Move focus to adjacent pane
Ctrl+B  →  T        # Open new tab
Ctrl+B  →  N        # Next tab
Ctrl+B  →  P        # Previous tab
```

---

## Keybindings

All commands use the **Ctrl+B** prefix followed by a key. Some commands also have direct shortcuts.

### Tab Management

| Keybinding | Direct Shortcut | Action |
|------------|----------------|--------|
| `Ctrl+B` → `T` | `Ctrl+Shift+T` | New tab |
| `Ctrl+B` → `W` | `Ctrl+Shift+W` | Close current tab |
| `Ctrl+B` → `N` | `Ctrl+Tab` | Next tab |
| `Ctrl+B` → `P` | `Ctrl+Shift+Tab` | Previous tab |
| `Ctrl+B` → `,` | — | Rename tab |

### Pane Management

| Keybinding | Action |
|------------|--------|
| `Ctrl+B` → `\` | Split pane left/right |
| `Ctrl+B` → `-` | Split pane top/bottom |
| `Ctrl+B` → `←↑→↓` | Move focus to pane in direction |
| `Ctrl+B` → `X` | Close current pane |

### Window Management

| Keybinding | Direct Shortcut | Action |
|------------|----------------|--------|
| `Ctrl+B` → `Shift+N` | `Ctrl+Shift+N` | New window |
| `F11` | — | Toggle fullscreen |

### Macros

| Keybinding | Action |
|------------|--------|
| `Ctrl+B` → `R` | Start/stop recording |
| `Ctrl+B` → `5` | Play last macro |
| `Ctrl+B` → `M` | Open macro manager |

### Session / Misc

| Keybinding | Action |
|------------|--------|
| `Ctrl+B` → `S` | Save session |
| `Ctrl+B` → `O` | Open session manager |

---

## Claude Code Integration

KMux integrates deeply with Claude Code.

### Activity Status Display

Each pane header shows the real-time Claude Code status on the right:

- **Working** — Claude is generating a response (spinner animation)
- **Waiting** — Claude is waiting for input
- **Idle** — Idle after the last task completed

### How It Works

1. On startup, KMux automatically registers hooks in `~/.claude/settings.json`.
2. Claude Code writes a file to `%TEMP%/kmux-status/<pane-id>` on each event.
3. `ClaudeActivityWatcher` monitors that directory and updates pane status accordingly.

### Opening Claude Code Tabs

You can open new tabs or windows directly with Claude Code:

- Right-click the new tab button in the toolbar
- Right-click a recent folder entry → "New Claude Tab" / "New Claude Window"

---

## Tab Management

- Click a tab in the tab bar to switch to it
- Use `Ctrl+B` → `,` to rename the active tab
- When Claude Code is active in a tab, the tab indicator shows a pulse animation

---

## Pane Splitting

Panes are managed as a binary tree. Each split is either left/right or top/bottom.

```
┌──────────┬──────────┐
│          │  Pane B  │
│  Pane A  ├──────────┤
│          │  Pane C  │
└──────────┴──────────┘
```

- Drag the splitter bar to resize panes.
- The pane header displays the current directory folder name.
- Click a pane or use keybindings to move focus.

---

## Macro System

A powerful macro system for recording and replaying terminal interactions.

### Recording

1. Press `Ctrl+B` → `R` to start recording (status bar shows recording indicator).
2. Perform the desired terminal actions.
3. Press `Ctrl+B` → `R` again to stop recording.

### Playback

- `Ctrl+B` → `5` — Play the most recent macro
- Select a specific macro in the Macro Manager and click Play

### Macro Manager

Open with `Ctrl+B` → `M`:

- Browse all saved macros
- Edit macro name, bound key, and timing preservation setting
- Inspect the full action sequence
- Delete macros

### Macro Action Types

| Type | Description |
|------|-------------|
| `KeyInput` | Raw key press or VT sequence |
| `Paste` | Instant text insert |
| `Delay` | Wait N milliseconds |
| `Resize` | Terminal resize |
| `NewPane` | Split a pane |
| `SwitchTab` | Jump to a tab by index |
| `RunCommand` | Type a command and press Enter |

---

## Session Management

Save the current multi-window/tab/pane layout and restore it later.

### Saving a Session

Press `Ctrl+B` → `S` to open a dialog for entering the session name.

What gets saved:
- Position and size of all windows
- Tab list and active tab for each window
- Pane split layout for each tab

### Session Manager

Open with `Ctrl+B` → `O`:

- Browse saved sessions (sorted newest first by save time)
- Load a session
- Delete a session

---

## Themes & Font Settings

### Built-in Themes

| Theme | Description |
|-------|-------------|
| Catppuccin Mocha | Default. Warm dark theme |
| Catppuccin Latte | Light theme |
| One Dark Pro | VS Code-style dark theme |
| Dracula | Purple-toned dark theme |
| Nord | Cool blue dark theme |
| Tokyo Night | Neon dark theme |

### Changing Settings

Click the settings (⚙) button in the toolbar:

- Select a theme (with color swatch previews)
- Choose terminal font family (default: Cascadia Code)
- Set terminal font size (default: 14)

---

## Recent Folders

Quickly open frequently used working directories.

### Access

Click the recent folders icon in the toolbar to open the folder list. Right-click any entry to see:

- Open as new tab
- Open as Claude tab
- Open in new window
- Open as Claude window in new window

### Recent Folders Manager

To edit the list directly:

- Add folder (via folder browser dialog)
- Remove individual entries
- Clear all entries (with confirmation)

Up to 20 directories are stored.

---

## Shell Profiles

KMux auto-detects installed shells on the system.

| Profile | Executable | Description |
|---------|-----------|-------------|
| PowerShell | `pwsh.exe` | PowerShell 7+ |
| PowerShell (Legacy) | `powershell.exe` | Windows PowerShell 5.1 |
| Command Prompt | `cmd.exe` | Default CMD |
| Git Bash | `bash.exe` | Git for Windows |
| WSL | `wsl.exe` | Windows Subsystem for Linux |
| Claude Code | `cmd.exe /k claude` | Claude Code CLI |

---

## Project Structure

```
KMux/
├── KMux.sln                    # Visual Studio solution
├── build.bat                   # Build script
└── src/
    ├── KMux.App/               # WPF app entry point
    │   ├── App.xaml(.cs)       # App init, shell detection, Claude hook registration
    │   └── Assets/
    │       ├── terminal.html   # xterm.js host page
    │       ├── xterm/          # xterm.js library
    │       └── hooks/          # Claude Code integration hook scripts
    │
    ├── KMux.Core/              # Shared models and interfaces
    │   └── Models/
    │       ├── AppSettings.cs       # User preferences (theme, font)
    │       ├── ShellProfile.cs      # Shell profile definitions
    │       ├── Session.cs           # Session/layout models
    │       ├── ThemeColors.cs       # Color palette (WPF + xterm.js)
    │       └── ITerminalProcess.cs  # Terminal process interface
    │
    ├── KMux.Terminal/          # Windows ConPty terminal backend
    │   └── ConPty/
    │       ├── ConPtyNativeMethods.cs  # P/Invoke wrappers
    │       ├── PseudoConsole.cs        # ConPty handle management
    │       ├── ConPtyProcess.cs        # ITerminalProcess implementation
    │       ├── ProcessFactory.cs       # Shell process spawning
    │       ├── ProcessUtils.cs         # Live CWD tracking via PEB reading
    │       └── ShellProfileDetector.cs # Auto-detect installed shells
    │
    ├── KMux.Layout/            # Pane layout tree management
    │   ├── LayoutNode.cs       # LeafNode / SplitNode hierarchy
    │   ├── LayoutTree.cs       # Tree mutations (split, close, resize, navigate)
    │   └── LayoutSerializer.cs # JSON serialization
    │
    ├── KMux.Session/           # Persistence (JSON file storage)
    │   ├── SessionStore.cs          # Save/load sessions
    │   ├── SettingsStore.cs         # Save/load app settings
    │   └── RecentDirectoryStore.cs  # Recent folders list
    │
    ├── KMux.Macro/             # Macro recording and playback
    │   ├── MacroAction.cs      # Action type hierarchy
    │   ├── MacroModel.cs       # Macro definition
    │   ├── MacroRecorder.cs    # Input recording engine
    │   ├── MacroPlayer.cs      # Async playback engine
    │   └── MacroStore.cs       # Macro file persistence
    │
    ├── KMux.Keybindings/       # Keyboard binding management
    │   ├── KeyChord.cs         # (Key, ModifierKeys) record
    │   ├── KeyBinding.cs       # Command definition
    │   └── KeyBindingMap.cs    # Ctrl+B prefix resolution engine
    │
    └── KMux.UI/                # WPF UI (MVVM)
        ├── ViewModels/
        │   ├── TerminalWindowViewModel.cs  # Root window logic
        │   ├── TabViewModel.cs             # Tab state and layout
        │   ├── TerminalViewModel.cs        # Single pane/shell state
        │   └── PaneViewModel.cs            # Pane UI state
        ├── Views/
        │   ├── TerminalWindow.xaml(.cs)     # Main window
        │   ├── PaneContainer.xaml(.cs)      # Dynamic layout builder
        │   ├── TerminalPane.xaml(.cs)       # Individual pane UI
        │   ├── MacroManagerWindow.xaml.cs   # Macro manager
        │   ├── SessionManagerWindow.xaml.cs # Session manager
        │   ├── SettingsWindow.xaml.cs       # Settings window
        │   └── RecentFoldersWindow.xaml.cs  # Recent folders manager
        └── Services/
            ├── AppSettingsService.cs    # Singleton theme/settings manager
            ├── ClaudeActivityWatcher.cs # Claude status file watcher
            ├── WebView2Bridge.cs        # C# <-> JS communication bridge
            ├── ThemeHelper.cs           # Color utilities
            └── ThemeResourceKeys.cs     # WPF resource key constants
```

---

## Architecture

### Hierarchy

```
TerminalWindow
  └── Tabs (ObservableCollection<TabViewModel>)
        └── Panes (binary tree layout)
              └── Terminal (xterm.js via WebView2)
```

### Core Design Patterns

- **MVVM** — `CommunityToolkit.Mvvm` `ObservableObject` and `RelayCommand` throughout
- **Binary Tree Layout** — Polymorphic `LeafNode` / `SplitNode` record hierarchy with JSON serialization
- **JSON Polymorphism** — `System.Text.Json` type discriminators for `LayoutNode` and `MacroAction`
- **C# ↔ JavaScript Bridge** — `WebView2Bridge` sends terminal output to and receives input from xterm.js
- **Event-Driven** — Loose coupling via `ClaudeActivityWatcher`, property change, and output events
- **Async I/O** — ConPty output pump runs on a dedicated `LongRunning` thread; UI updates marshaled via `Dispatcher`

### ConPty Terminal Data Flow

```
ShellProcess (cmd/pwsh/wsl)
    ↕ (stdin/stdout pipes)
ConPtyProcess  <-->  PseudoConsole (Windows ConPty API)
    ↓ OutputReceived event
WebView2Bridge  -->  xterm.js (JavaScript)
    ↑ KeyInput from user
WebView2Bridge  <--  xterm.js onData callback
    ↓
ConPtyProcess.Write()
```

---

## Configuration File Locations

All settings are stored under `%APPDATA%\KMux\`.

| Path | Contents |
|------|----------|
| `settings.json` | App settings (theme, font family, font size) |
| `sessions/` | Session JSON files (one per Guid) |
| `macros/` | Macro JSON files (one per Guid) |
| `recent_dirs.json` | Recent folders list (up to 20 entries) |

Claude Code hook files:

| Path | Contents |
|------|----------|
| `~/.claude/settings.json` | Claude Code hook registrations |
| `%TEMP%/kmux-status/` | Claude activity status files (one per pane ID) |
