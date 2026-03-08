# Panda Player - Architecture Document

## System Overview

Panda Player is a layered, service-oriented desktop application for efficient video library management. It separates concerns across three main layers:

```
┌─────────────────────────────────────┐
│         WPF User Interface          │  - MainWindow, Controls, ViewModels
│     (PandaPlayer.UI)                │  - XAML Layout, Event Handlers
├─────────────────────────────────────┤
│         Service Layer               │  - PlayerService, SessionService
│     (PandaPlayer.Core.Services)     │  - FileMoveService, SettingsService
├─────────────────────────────────────┤
│         Data Layer                  │  - Models, Persistence Stores
│   (PandaPlayer.Core Models)         │  - Event Definitions
└─────────────────────────────────────┘
         ↓
    LibVLC Engine (C/C++)
    │
    └─→ D3D11/DXVA (Hardware Acceleration)
```

## Layer Descriptions

### 1. Presentation Layer (PandaPlayer.UI)

**Responsibilities:**
- Window layout and controls (WPF XAML)
- User input handling (keyboard, mouse, UI interactions)
- Real-time UI updates reflecting playback state
- Job progress visualization
- Settings UI presentation

**Key Components:**
- `MainWindow.xaml`: Main application interface
- `Views/`: Dialogs and custom controls
- `ViewModels/`: MVVM viewmodels for data binding
- `Controls/`: Reusable UI components (ProgressBars, JobListItems, etc.)

**Technology:**
- WPF (.NET 8 Windows Desktop)
- System.Reactive for event subscriptions
- Data binding for real-time updates

### 2. Service Layer (PandaPlayer.Core.Services)

**Responsibilities:**
- Business logic implementation
- Orchestration of playback, sessions, file operations
- Event publishing to UI layer
- External resource management (VLC, file system)

**Core Services:**

```
IPlayerService
├─ Current implementation: VlcPlayerService
├─ Responsibilities:
│  ├─ PlayAsync() / StopAsync()
│  ├─ SeekAsync() with event publishing
│  ├─ Volume/Fullscreen control
│  └─ Hardware acceleration management
└─ Events:
   ├─ PlaybackStarted
   ├─ PlaybackEnded
   ├─ PlaybackError
   ├─ PositionChanged
   └─ PlaybackPaused

IPlaybackSessionService
├─ Current implementation: PlaybackSessionService
├─ Responsibilities:
│  ├─ CreateSessionAsync(folderPath)
│  ├─ GetNextVideo() / GetPreviousVideo()
│  ├─ SetPlaybackModeAsync(mode)
│  ├─ Shuffle-no-repeat algorithm
│  └─ Session lifecycle management
└─ Features:
   ├─ Recursive folder scanning
   ├─ Video list management
   ├─ Played state tracking
   └─ Mode persistence

IFileMoveService
├─ Current implementation: FileMoveService
├─ Responsibilities:
│  ├─ Safe copy pipeline
│  ├─ Destination verification
│  ├─ Concurrent job queue
│  ├─ Progress tracking
│  └─ Conflict detection
└─ Safety Features:
   ├─ Temporary file (.panda.partial)
   ├─ SHA-256 checksum verification
   ├─ Size matching validation
   ├─ Source deletion only after verification
   └─ Conflict resolution UI

IScreenshotService
├─ Current implementation: ScreenshotService
├─ Responsibilities:
│  ├─ CaptureScreenshotAsync()
│  ├─ Output folder management
│  └─ File naming with timestamps
└─ Features:
   ├─ Auto-save to configured folder
   ├─ Timestamped filenames
   └─ Event notification on capture

IAppSettingsStore
├─ Current implementation: JsonAppSettingsStore
├─ Responsibilities:
│  ├─ LoadSettingsAsync() / SaveSettingsAsync()
│  ├─ Settings validation
│  └─ Default configuration
└─ Storage:
   └─ %APPDATA%\PandaPlayer\settings.json

IPlaybackStateStore
├─ Current implementation: JsonPlaybackStateStore
├─ Responsibilities:
│  ├─ LoadSessionAsync() / SaveSessionAsync()
│  ├─ DeleteSessionAsync()
│  └─ Session enumeration
└─ Storage:
   └─ %APPDATA%\PandaPlayer\sessions\{hash}.json
```

### 3. Data Layer (PandaPlayer.Core)

**Responsibilities:**
- Data model definitions
- Persistence abstraction (stores)
- Event definitions
- Business rules enforcement

**Data Models:**

```
VideoItem
├─ FilePath: Full path to video file
├─ FileName: Display name
├─ FolderPath: Parent folder
├─ FileSizeBytes: File size
├─ Duration: Video length
├─ IsPlayed: Completion state
├─ LastPlayedAt: Timestamp
└─ PlaybackIndex: Order in playlist

PlaybackSession
├─ SessionId: Unique identifier
├─ RootFolderPath: Session folder
├─ AllVideos: VideoItem[]
├─ CurrentVideoIndex: Current position
├─ Mode: Sequential or Shuffle
├─ TotalVideosPlayed: Progress counter
├─ CreatedAt / LastModifiedAt: Timestamps
└─ Computed:
   ├─ TotalVideos (AllVideos.Count)
   └─ RemainingVideos (Total - Played)

FileMoveJob
├─ JobId: Unique identifier
├─ SourceFilePath: Source location
├─ DestinationFolderPath: Destination
├─ Status: Pending|Copying|Verifying|Finalizing|Completed|Failed|Cancelled|ConflictDetected
├─ ProgressPercentage: 0-100
├─ BytesCopied / TotalBytes: Progress
├─ SpeedMbps: Transfer speed
├─ EstimatedTimeRemaining: ETA
├─ ErrorMessage: Failure details
├─ ConflictResolution: User choice on conflict
└─ Timestamps: StartedAt, CompletedAt

PlaybackSettings
├─ SeekForwardSeconds: Jump duration
├─ SeekBackwardSeconds: Jump duration
├─ Volume: 0.0-1.0
├─ HardwareAccelerationEnabled: Boolean
├─ ScreenshotFolder: Output path
├─ MoveTargetFolders: Dict<int, string> (1-9 slots)
├─ DefaultPlaybackMode: Sequential or Shuffle
└─ KeyboardShortcuts: Dict<string, string> (action→key)

FileMoveConfiguration
├─ VerifyChecksum: SHA-256 validation
├─ BufferSizeBytes: Copy buffer (default 1MB)
├─ MaxConcurrentMoves: Job queue limit (default 2)
├─ PartialFileExtension: Temp suffix (.panda.partial)
└─ DeleteSourceAfterVerification: Safety flag
```

## Event Flow Architecture

### Playback Event Flow

```
User Input (Space)
  ↓
MainWindow.KeyDown handler
  ↓
PlayerService.PlayAsync(VideoItem)
  ↓
LibVLC PlayRequest
  ↓
PlayerService.OnMediaPlaying event
  ↓
PlayerService.PlaybackStarted event
  ↓
UI ViewModel receives event
  ↓
MainWindow UI updates (display video, show controls)
  ↓
PlayerService.PositionChanged fires periodically
  ↓
UI ViewModel updates progress bar
  ↓
Player detects end of video
  ↓
PlayerService.PlaybackEnded event
  ↓
SessionService.MarkVideoAsPlayedAsync()
  ↓
StateStore.SaveSessionAsync() (persistence)
  ↓
SessionService.GetNextVideo() for queue management
```

### File Move Event Flow

```
User Input (Ctrl+1 / Right-click Move)
  ↓
MainWindow triggers FileMoveService.MoveFileAsync()
  ↓
FileMoveService creates FileMoveJob
  ↓
ExecuteSafeMoveAsync() on background thread
  ├─ FileMoveService.JobStarted event
  ├─ OpenRead(sourceFile)
  ├─ Create(destFile.panda.partial)
  ├─ [Loop] Read → Write with progress updates
  │  └─ FileMoveService.JobProgress event (periodically)
  ├─ Close file handles
  ├─ VerifyCopyAsync()
  │  ├─ Size comparison
  │  └─ SHA-256 checksum validation
  ├─ Conflict check
  ├─ File.Move() to final name
  ├─ File.Delete(source) [ONLY if above succeeds]
  └─ FileMoveService.JobCompleted event
  ↓
UI ViewModel receives completion event
  ↓
Update Jobs panel, remove from active list
```

## Shuffle-No-Repeat Algorithm

The shuffle-no-repeat behavior is implemented in `PlaybackSessionService.GetShuffleNextVideo()`:

```csharp
Algorithm: Shuffle without repeat in cycle

State:
  _playedIndicesInCurrentCycle: HashSet<int>  // Tracks indices in current cycle
  _currentSession.AllVideos: List<VideoItem>  // All videos in session

Logic:
  1. Check if cycle complete:
     IF _playedIndicesInCurrentCycle.Count >= AllVideos.Count:
       CLEAR _playedIndicesInCurrentCycle
       // Cycle complete - prompt user for new cycle

  2. Find unplayed videos:
     unplayedIndices = []
     FOR i = 0 TO AllVideos.Count:
       IF i NOT IN _playedIndicesInCurrentCycle:
         ADD i to unplayedIndices

  3. Random selection:
     IF unplayedIndices is empty:
       RETURN null  // All played
     randomIndex = unplayedIndices[random(0, length)]

  4. Track and return:
     SET _currentSession.CurrentVideoIndex = randomIndex
     ADD randomIndex to _playedIndicesInCurrentCycle
     RETURN AllVideos[randomIndex]

Guarantees:
  - No video repeats before all others played once
  - Every cycle starts fresh
  - End-of-cycle condition detectable
```

## Safe File Move Pipeline

The FileMoveService implements a verified, safety-first approach:

```
Phase 1: Initialize
  Input: sourceFilePath, destinationFolderPath
  Check: File exists, destination writable, no locks
  Acquire: Semaphore (respects MaxConcurrentMoves)

Phase 2: Copy to Temporary
  Create: tempFilePath = destPath + ".panda.partial"
  Copy: buffer.CopyAsync(source → temp)
  Progress: Update BytesCopied, Speed, ETA
  Monitor: Allow cancellation during copy

Phase 3: Verification
  Check: File sizes match
  Check: SHA-256 checksums match (if enabled)
  Result: PASS or report error
  If FAIL: Delete temp file, report to user

Phase 4: Conflict Detection
  Check: Does finalPath already exist?
  If YES: Emit JobConflict event, wait for resolution
  Resolution: Rename | Overwrite | Skip
  If Rename: Append timestamp to filename
  If Overwrite: Warn user, proceed (source preserved until confirmed)

Phase 5: Finalization
  Rename: tempFile → finalFile
  Check: Rename succeeded
  If FAIL: Report error, preserve source and temp

Phase 6: Source Deletion
  Delete: sourceFile
  ONLY after all above phases succeed
  If DELETE fails: Log warning, destination is safe

Phase 7: Completion
  Status: Completed
  Emit: JobCompleted event
  Move to: _completedJobs list
  Release: Semaphore
```

**Safety Guarantees:**
- Source never deleted before destination verified
- Temporary files cleaned up on failure
- Checksum prevents silent corruption
- Conflict detection prevents overwrites
- Cancellation support preserves all files
- Semaphore prevents resource exhaustion

## Persistence Architecture

### Settings Store (JSON)

```
File: %APPDATA%\PandaPlayer\settings.json

Lifecycle:
  OnAppStartup:
    JsonAppSettingsStore.LoadSettingsAsync()
    IF file missing: return GetDefaultSettings()
    DESERIALIZE from JSON
    VALIDATE values

  OnSettingChange:
    Update PlaybackSettings object in memory
    JsonAppSettingsStore.SaveSettingsAsync()
    SERIALIZE to JSON
    WRITE to file

  OnReset:
    DELETE settings.json
    Restart app for defaults
```

### Session Store (JSON per Folder)

```
Directory: %APPDATA%\PandaPlayer\sessions\

Filename Strategy:
  Hash = SHA256(folderPath.ToLower())
  FileName = "session_{hash.Substring(0,16)}.json"
  Purpose: Unique per folder, deterministic

Lifecycle:
  OnFolderOpen:
    sessionHash = SHA256(folderPath)
    sessionPath = sessions\ + "session_{hash}.json"
    IF file exists:
      JsonPlaybackStateStore.LoadSessionAsync()
      DESERIALIZE PlaybackSession
      RESTORE videos, currentIndex, played state
    ELSE:
      CREATE new PlaybackSession
      SCAN folder recursively for videos
      INITIALIZE with default mode

  OnVideoCompleted:
    PlaybackSessionService.MarkVideoAsPlayedAsync()
    Set IsPlayed = true, LastPlayedAt = DateTime.Now
    Increment TotalVideosPlayed
    JsonPlaybackStateStore.SaveSessionAsync()
    WRITE updated session to JSON

  OnResetProgress:
    PlaybackSessionService.ResetProgressAsync()
    Set IsPlayed = false for all videos
    Set TotalVideosPlayed = 0
    JsonPlaybackStateStore.SaveSessionAsync()

  OnReset:
    JsonPlaybackStateStore.DeleteSessionAsync()
    DELETE session file
    New session created on next folder open
```

## Dependency Injection Pattern

Services are initialized in `MainWindow.xaml.cs`:

```csharp
private void InitializeServices()
{
    // Create stores
    var appDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PandaPlayer"
    );
    var settingsStore = new JsonAppSettingsStore(appDataPath);
    var stateStore = new JsonPlaybackStateStore(appDataPath);

    // Load settings
    _settings = await settingsStore.LoadSettingsAsync();

    // Create services
    _playerService = new VlcPlayerService();
    _sessionService = new PlaybackSessionService(stateStore);
    _fileMoveService = new FileMoveService(
        new FileMoveConfiguration
        {
            VerifyChecksum = _settings.VerifyChecksum ?? true,
            MaxConcurrentMoves = _settings.MaxConcurrentMoves ?? 2
        }
    );
    _screenshotService = new ScreenshotService(
        _settings.ScreenshotFolder,
        _playerService._mediaPlayer
    );

    // Wire up event handlers
    _playerService.PlaybackEnded += OnPlaybackEnded;
    _fileMoveService.JobProgress += OnJobProgress;
    _sessionService.SessionCreated += OnSessionCreated;
}
```

## Testing Strategy

### Unit Tests

1. **ShuffleNoRepeatTests**
   - Test no repeat in single cycle
   - Test cycle completion detection
   - Test transition between cycles

2. **PlaybackStateTests**
   - Test save/load session persistence
   - Test settings save/load
   - Test reset operation

3. **SafeFileMoveTests**
   - Test complete move pipeline
   - Test conflict detection
   - Test verification failure handling

### Integration Tests (Future)

- Full playback session lifecycle
- Multi-video folder with mixed formats
- Concurrent move operations
- Crash recovery and session restoration

## Performance Considerations

### Memory Management

- Lazy-load video metadata (avoid loading all videos at once for huge folders)
- Stream file copy operations (don't buffer entire files in RAM)
- Dispose VLC resources properly on close

### Threading

- File move operations on background thread (ThreadPool)
- UI updates marshalled to UI thread (Dispatcher)
- Semaphore limits concurrent operations

### Scalability

- Tested with 1000+ video folders
- Session index by folder hash (O(1) lookup)
- Shuffle algorithm O(n) where n = unplayed videos

## Error Handling Strategy

```
Playback Errors:
  FileNotFound → Show error dialog, offer alternative
  CodecNotSupported → Suggest format conversion
  HardwareFailure → Fall back to software decode
  CorruptFile → Skip, continue to next video

Move Errors:
  SourceLocked → Retry automatically or offer manual retry
  DiskFullDest → Show space requirement, cancel
  PermissionDenied → Show as permission error, cancel
  Interrupted → Clean temp file, preserve source

Persistence Errors:
  FilePermission → Log warning, use defaults
  CorruptJSON → Log warning, recover with defaults
  DiskFull → Show critical error, stop operations
```

## Security Considerations

1. **File Operations**
   - Never use UNC paths without validation
   - Path traversal prevention in file operations
   - Verify file operations stay in user's directories

2. **Verification**
   - Checksum verification prevents silent corruption
   - Size checks catch incomplete copies
   - Temporary file isolation prevents conflicts

3. **Persistence**
   - Settings stored in user's AppData (inherits OS permissions)
   - No sensitive data stored unencrypted
   - Session files contain only playback metadata

## Future Architecture Improvements

1. **Plugin System** - Allow extensions for custom filters, metadata sources
2. **Async Service Initialization** - Better startup performance
3. **Cache Layer** - Optimize repeated folder scans
4. **Event Sourcing** - Audit trail for file operations
5. **Remote Playback** - Network video streaming support

---

Document Version: 1.0  
Last Updated: 2026-02-24
