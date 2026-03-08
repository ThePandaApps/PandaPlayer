# Codex Player - Professional Video Manager for Windows

**Codex Player** is a production-ready VLC-based video player designed for efficient review and triage of large video libraries. It emphasizes keyboard-first controls, safe batch file operations, and professional-grade features for streamlined video management workflows.

## 🎯 Project Overview

Codex Player combines the power of VLC's robust playback engine with a modern Windows UI specifically optimized for video curators, editors, and content managers. It enables rapid folder-based playback with advanced shuffle modes, background file move operations with data verification, and comprehensive session persistence.

### Key Differentiators

- **Safe File Move Pipeline**: Never deletes source files until destination is fully verified
- **Shuffle-No-Repeat Mode**: Randomizes playback while guaranteeing no repeats within a cycle
- **Complete Persistence**: Remembers your playback progress per folder tree across restarts
- **Keyboard-Centric Workflow**: Every major action has a keyboard shortcut
- **Background Jobs**: Move files without pausing playback
- **Hardware Acceleration**: Leverages D3D11/DXVA for efficient 4K playback on lower-capacity machines

## 📋 Features

### Playback Capabilities

- **Supported Formats**: MP4, MKV, AVI, MOV, WMV, WEBM, M4V, TS, M2TS, FLV
- **Video Codecs**: H.264 and H.265 via VLC engine
- **Hardware Acceleration**: D3D11/DXVA enabled by default for Windows
- **Playback Modes**: Sequential and Shuffle (with no-repeat guarantee)
- **4K Support**: Optimized for 4K video playback on standard hardware

### Control System

#### Transport Controls
- **Play/Pause**: Space
- **Next**: N
- **Previous**: P
- **Stop**: ESC
- **Fullscreen**: F

#### Seek Controls  
- **Seek Forward**: Right Arrow (configurable duration, default 5s)
- **Seek Backward**: Left Arrow (configurable duration, default 5s)
- **Volume Up**: Up Arrow
- **Volume Down**: Down Arrow

#### Content Management
- **Screenshot**: Ctrl+S (auto-saves to configured folder)
- **Move to Target 1-9**: Ctrl+1 through Ctrl+9
- **Settings**: Ctrl+, (comma)

### Folder Playback & Shuffle

- **Recursive Scanning**: Automatically discovers all videos in subfolders
- **Total Count Display**: Shows total videos including nested folders
- **Sequential Mode**: Plays videos in alphabetical order
- **Shuffle Mode**: Randomizes order while preventing repeats within a cycle
  - When all videos are played once, prompts to restart a new cycle
  - Guarantees no video plays twice before all others play once
- **Progress Tracking**: Real-time display of total, played, and remaining counts
- **Current Mode Display**: UI shows active playback mode at all times

### Advanced File Operations

#### Move-to-Folder Workflow
- **Configurable Targets**: Define up to 9 destination folders
- **Keyboard Shortcuts**: Quick move via Ctrl+1 through Ctrl+9
- **Background Operation**: Continue playback while moving files
- **Progress Monitoring**: Real-time updates on:
  - Copy percentage
  - Transfer speed (MB/s)
  - Estimated time remaining
  - Job status and errors

#### Safe Move Pipeline (CRITICAL)
The move operation follows a verified, safety-first sequence:

1. **Pause/Release**: Stop playback to release file handle
2. **Copy to Temporary**: Write to destination as `.codex.partial`
3. **Verify Copy**: Size validation + optional checksum (SHA-256)
4. **Check Conflicts**: Detect if file already exists at destination
5. **Finalize**: Rename partial file to final destination name
6. **Delete Source**: Only remove source after successful verification

**Safety Guarantees:**
- Source file never deleted before destination fully copies and verifies
- If interrupted or failed, source remains completely intact
- Conflict detection prevents data overwrite
- Supports retry and conflict resolution

### Screenshot Capture

- **Auto-Shot**: Ctrl+S captures current video frame
- **Auto-Save**: Screenshots saved to configured folder with timestamp
- **Naming**: `{video_name}_{timestamp}.png`
- **Folder Configuration**: Customizable output directory

### Session Persistence

- **Progress Tracking**: Remember which videos you've played per folder
- **Automatic Save**: Progress saved after each video mark
- **Mode Retention**: Recalls your preferred playback mode per session
- **Reset Option**: Clear progress for a session if needed
- **Recovery**: Restore previous session on app restart

## 🏗️ Architecture

### Project Structure

```
PandaPlayer/
├── PandaPlayer.sln              # Visual Studio solution
├── PandaPlayer.Core/            # Core business logic
│   ├── Models/                  # Data models (VideoItem, FileMoveJob, etc.)
│   ├── Services/                # Service interfaces and implementations
│   ├── Events/                  # Custom event definitions
│   └── Persistence/             # State and settings stores
├── PandaPlayer.UI/              # WPF user interface
│   ├── Views/                   # XAML windows and controls
│   ├── ViewModels/              # MVVM view models
│   └── Controls/                # Custom UI controls
├── PandaPlayer.Tests/           # Unit and integration tests
│   ├── Unit/                    # Unit test suites
│   └── Integration/             # Integration test scenarios
├── PandaPlayer.Launcher/        # Application entry point
└── Scripts/                     # Build and setup scripts
    ├── build.bat                # Windows build
    ├── build.sh                 # Cross-platform build
    ├── setup-explorer.bat       # Register Explorer context menu
    └── uninstall-explorer.bat   # Remove Explorer integration
```

### Core Services

#### IPlayerService / VlcPlayerService
Wraps LibVLC for video playback control. Manages media engine lifecycle, playback states, and hardware acceleration configuration.

**Key Methods:**
- `PlayAsync(VideoItem)` - Start playback of a video
- `PauseAsync()` / `ResumeAsync()` - Pause/resume playback
- `SeekAsync(long positionMs)` - Jump to position in video
- `StopAsync()` - Stop and release resources

#### IPlaybackSessionService / PlaybackSessionService  
Manages folder-based playback queues and shuffle modes with no-repeat guarantee.

**Key Features:**
- Recursively scans folders for video files
- Implements shuffle-no-repeat algorithm
- Tracks played status per video
- Manages session lifecycle

#### IFileMoveService / FileMoveService
Implements safe, verified background file move operations with conflict detection.

**Key Features:**
- Concurrent move job queue (configurable limit)
- Progress tracking with speed and ETA
- Conflict detection and resolution UI
- Checksum verification for copy integrity
- Never deletes source before destination verified

#### IScreenshotService / ScreenshotService
Captures video frames and saves to configured folder with metadata.

#### IAppSettingsStore / JsonAppSettingsStore
Persists user preferences (seek times, screenshot folder, move targets, shortcuts) to JSON files.

#### IPlaybackStateStore / JsonPlaybackStateStore
Saves/restores playback progress per folder session with hashed folder path indexing.

## 🛠️ Setup & Installation

### Prerequisites

- **Windows 10/11** (64-bit)
- **.NET 8 Runtime** - [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)
- **VLC Libraries** - Automatically included via LibVLCSharp NuGet package
- **Visual Studio 2022** (for development, optional for end-users)

### Building from Source

#### Windows

```bash
# Navigate to project root
cd PandaPlayer

# Run build script
Scripts\build.bat

# Output will be in: publish/ui/
```

#### macOS/Linux (for cross-platform development)

```bash
cd PandaPlayer
bash Scripts/build.sh
```

### Running the Application

After building:

```bash
# From the project root
PandaPlayer\PandaPlayer.UI\bin\Release\net8.0-windows\PandaPlayer.UI.exe
```

### Explorer Integration (Optional)

To add "Play with Codex Player" to Windows Explorer context menus:

```bash
Scripts\setup-explorer.bat "path\to\PandaPlayer.UI.exe"
```

**Registered Menus:**
- Right-click any video file → "Play with Codex Player"
- Right-click any folder → "Play Folder with Codex Player"

To remove Explorer integration:

```bash
Scripts\uninstall-explorer.bat
```

## 📖 Usage Guide

### Basic Playback

1. **Launch Application**
   ```
   PandaPlayer.UI.exe
   ```

2. **Open Video**
   - From menu: File → Open Video
   - Keyboard: Ctrl+O
   - Explorer: Right-click video → "Play with Codex Player"

3. **Open Folder**
   - From menu: File → Open Folder
   - Keyboard: Ctrl+Shift+O
   - Explorer: Right-click folder → "Play Folder with Codex Player"

### Keyboard Shortcuts

| Action | Shortcut | Customizable |
|--------|----------|-------------|
| Play/Pause | Space | Yes |
| Next Video | N | Yes |
| Previous Video | P | Yes |
| Seek Forward | Right Arrow | Yes |
| Seek Backward | Left Arrow | Yes |
| Screenshot | Ctrl+S | Yes |
| Move to Target 1-9 | Ctrl+1 through Ctrl+9 | Yes |
| Fullscreen | F | Yes |
| Volume Up | Up Arrow | Yes |
| Volume Down | Down Arrow | Yes |
| Settings | Ctrl+, | Yes |

### Shuffle Mode Workflow

1. Click **Mode Selector** in Session panel
2. Select **Shuffle**
3. Videos play in random order, never repeating within the cycle
4. When all videos played:
   - UI shows: "Cycle Complete"
   - Dialog prompt: "Start new cycle?"
   - Options: "Yes" (restart shuffle) / "No" (stop playback)

### Using Move Targets

**Configure Targets:**
1. Open Settings (Ctrl+,)
2. Find "Move Targets" section
3. For each slot (1-9):
   - Click folder selector button
   - Choose destination folder
   - Click Save

**Move a Video:**
1. While video is playing, press Ctrl+1 through Ctrl+9
2. Playback continues uninterrupted
3. Move job appears in "Jobs" panel
4. Monitor progress: percentage, speed, ETA
5. On completion: source deleted automatically

### Handling Move Conflicts

If destination file exists:
1. Conflict dialog appears
2. Choose option:
   - **Rename**: Add timestamp to filename
   - **Overwrite**: Replace existing file (source preserved until confirmed)
   - **Skip**: Cancel move, keep source intact

### Screenshot Capture

1. Configure screenshot folder in Settings
2. While playing, press Ctrl+S
3. Frame captured and saved as: `{video_name}_{YYYYMMDD_HHmmss}.png`
4. Notification shows save path

### Session Management

**Resume Previous Session:**
- Launch app
- It automatically loads last played folder's progress
- Resume from where you left off

**Reset Session Progress:**
1. Open a folder session
2. Click "Reset Progress" button in Session panel
3. Confirm action
4. All played markers cleared
5. Progress counter returns to 0/total

## 🧪 Testing

### Running Tests

```bash
# Run all tests
dotnet test PandaPlayer.sln -c Release

# Run specific test class
dotnet test PandaPlayer.sln -c Release --filter ClassName

# Run with detailed output
dotnet test PandaPlayer.sln -c Release --verbosity detailed
```

### Test Coverage

#### Unit Tests

**ShuffleNoRepeatTests.cs**
- ✅ Shuffle-no-repeat never repeats before all videos played
- ✅ End-of-cycle detection
- ✅ Multiple cycle transitions

**PlaybackStateTests.cs**
- ✅ Playback progress persists after app restart
- ✅ Reset progress clears all data
- ✅ Settings with move targets save correctly

**SafeFileMoveTests.cs**
- ✅ Safe move completes pipeline without deleting source prematurely
- ✅ Conflict detection and handling
- ✅ File move configuration has safe defaults

## 🔧 Configuration

### Settings File Location

```
%APPDATA%\PandaPlayer\settings.json
```

### Default Settings Structure

```json
{
  "SeekForwardSeconds": 5,
  "SeekBackwardSeconds": 5,
  "Volume": 1.0,
  "HardwareAccelerationEnabled": true,
  "ScreenshotFolder": "C:\\Users\\YourUser\\Pictures\\PandaPlayer",
  "MoveTargetFolders": {
    "1": "C:\\Videos\\Review",
    "2": "C:\\Videos\\Approved"
  },
  "DefaultPlaybackMode": "Sequential",
  "KeyboardShortcuts": {
    "PlayPause": "Space",
    "Next": "N",
    "Previous": "P",
    "SeekForward": "Right",
    "SeekBackward": "Left",
    "Screenshot": "Ctrl+S"
  }
}
```

### Session Progress Storage

```
%APPDATA%\PandaPlayer\sessions\{hash}.json
```

Each folder gets a session file named with a SHA-256 hash of the folder path for unique tracking.

## 🚀 Advanced Features

### Hardware Acceleration

Codex Player defaults to **D3D11/DXVA** for Windows, enabling efficient 4K video playback.

To disable (if experiencing issues):
1. Edit settings.json
2. Set `"HardwareAccelerationEnabled": false`
3. Restart application

### Concurrent Move Operations

By default, **2 concurrent move jobs** run. To adjust:

1. Edit settings.json
2. Add/modify: `"MaxConcurrentMoves": 4` (example)
3. Restart application

### Checksum Verification

Move operations verify file integrity using SHA-256 checksums by default.

To disable (faster, less safe):
1. Edit settings.json
2. Add: `"VerifyChecksum": false`
3. Restart application

**⚠️ Warning:** Disabling checksum verification reduces safety. Only disable if you trust your storage devices.

## 📊 Performance Characteristics

### Tested Scenarios

| Scenario | Performance | Notes |
|----------|-------------|-------|
| 4K H.265 playback | Smooth (60fps) | D3D11 acceleration required |
| Folder with 1000 videos | <2s scan | Recursive with subfolder count |
| Move 2GB file | ~100-150 MB/s | Depends on storage speed |
| Shuffle 500 videos | No repeat guarantee | Cycle completes in <3s |
| Session load/save | <100ms | JSON serialization |

## 🐛 Troubleshooting

### Playback Issues

**Video plays with no sound:**
- Update Windows audio drivers
- Check volume slider in app
- Try different video file (may be codec issue)

**Video stutters or lags:**
- Enable hardware acceleration in settings
- Close other applications
- Reduce video resolution if possible

**4K video won't play:**
- Verify GPU supports H.265 decode
- Update graphics drivers
- Try H.264 video for testing

### Move Operations

**Move stuck at "Verifying":**
- Check destination disk space
- Verify file isn't locked by another process
- Cancel job and retry

**"Conflict detected" on move:**
- Choose "Rename" to auto-fix
- Or manually delete existing file
- Then retry move

**"Source file could not be deleted" warning:**
- Source was successfully copied
- File is locked by another application
- Close other file explorers and retry

### Session/Settings

**Progress doesn't persist:**
- Verify %APPDATA%\PandaPlayer is writable
- Check disk space
- Try resetting settings and restarting

**Settings reverted to defaults:**
- settings.json was corrupted
- App auto-recovers with defaults
- Reconfigure and save

## 📝 Development

### Code Style

- **C# version**: C# 11 (latest stable)
- **Naming**: PascalCase for classes/methods, camelCase for locals
- **Architecture**: MVVM for UI, DI-compatible services
- **Dependencies**: Minimal, only required packages

### Adding New Features

1. **Add to Core**: Define interface in `PandaPlayer.Core.Services`
2. **Implement Service**: Create implementation in same namespace
3. **Register in UI**: Add to MainWindow service initialization
4. **Add Tests**: Create test class in `PandaPlayer.Tests.Unit`
5. **Update Docs**: Modify this README

### Debugging

```bash
# Run with debug console output
dotnet run --project PandaPlayer.UI --configuration Debug

# Attach Visual Studio debugger
# Open PandaPlayer.sln in Visual Studio → F5
```

## 📦 Dependencies

### NuGet Packages

- **LibVLCSharp** (3.7.0): VLC media playback engine wrapper
- **System.Reactive** (5.4.1): Event-driven programming patterns
- **Newtonsoft.Json** (13.0.3): JSON serialization for persistence
- **xunit** (2.6.6): Unit testing framework
- **Moq** (4.20.70): Mocking library for tests

All packages are managed through NuGet and specified in `.csproj` files.

## 📄 License

Codex Player is built upon VLC architecture. VLC and LibVLCSharp are licensed under LGPL v2.1+. See VLC documentation for licensing details.

Codex Player application code is provided as-is for professional video management workflows.

## 🤝 Contributing

To contribute improvements:

1. Fork the repository
2. Create feature branch: `git checkout -b feature/my-feature`
3. Implement with tests
4. Run full test suite: `dotnet test`
5. Submit pull request with detailed description

## 📞 Support & Issues

For issues, feature requests, or questions:

1. Check Troubleshooting section above
2. Review existing issues
3. Provide:
   - Windows version
   - .NET runtime version
   - Steps to reproduce
   - Video file information (format, codec, resolution)

## 🎓 Architecture Decision Records

### Why VLC?
- Robust, production-proven playback engine
- Excellent codec support (H.264, H.265, many others)
- Hardware acceleration on Windows (D3D11/DXVA)
- Open source and well-maintained

### Why WPF?
- Native Windows platform integration
- Hardware-accelerated rendering
- Modern UI capabilities
- Strong MVVM support

### Why JSON for Persistence?
- Human-readable for debugging
- No external database dependencies
- Easy to backup/restore
- Adequate performance for session/settings scale

### Why Shuffle-No-Repeat?
- Professional reviewers need systematic coverage
- Random order prevents bias
- No-repeat guarantee ensures thoroughness before cycling
- Better than other randomization approaches

## 🎯 Roadmap

### Phase 1 (Current - MVP)
- ✅ Core VLC integration
- ✅ Folder playback with shuffle-no-repeat
- ✅ Safe file move pipeline
- ✅ Keyboard shortcuts
- ✅ Session persistence

### Phase 2 (Next)
- 🔄 Editable keyboard shortcut mapping UI
- 🔄 Playlist management (create custom playlists)
- 🔄 Video metadata extraction and display
- 🔄 Batch operations (move/delete multiple videos)

### Phase 3 (Future)
- 🔮 Watch folders for auto-import
- 🔮 Tagging and metadata editing
- 🔮 Thumbnail preview strips
- 🔮 Performance analytics and reporting
- 🔮 Network playback capabilities

## 📚 Additional Resources

- [VLC Documentation](https://www.videolan.org/vlc/index.html)
- [LibVLCSharp GitHub](https://github.com/mfkl/libvlcsharp)
- [Microsoft WPF Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)

---

**Codex Player** - Professional Video Management Made Simple.
Built for people who review and manage large video libraries efficiently.

**Version: 1.0.0-alpha**  
**Last Updated: 2026-02-24**
