# Panda Player - Feature Specification

## 1. Core Playback Features

### 1.1 Video Format Support

| Format | Extension | Codec Support | Status |
|--------|-----------|---------------|--------|
| MP4 | .mp4 | H.264, H.265, AAC, MP3 | ✅ Supported |
| Matroska | .mkv | H.264, H.265, VP8, VP9 | ✅ Supported |
| AVI | .avi | MPEG-2, MPEG-4, WMV | ✅ Supported |
| MOV | .mov | H.264, H.265, ProRes | ✅ Supported |
| WMV | .wmv | VC-1, MPEG-4 | ✅ Supported |
| WebM | .webm | VP8, VP9 | ✅ Supported |
| MPEG-4 Audio | .m4v | H.264, AAC | ✅ Supported |
| MPEG-TS | .ts | H.264, H.265, MPEG-2 | ✅ Supported |
| MPEG-TS Video | .m2ts | H.264, H.265 | ✅ Supported |
| Flash Video | .flv | VP6, Sorenson | ✅ Supported |

**Implementation:** LibVLCSharp provides codec support via underlying VLC engine.

### 1.2 Playback Controls

**Transport Controls:**
- **Play**: Start playback from current position
- **Pause**: Temporarily stop playback, retain position
- **Resume**: Continue from paused position
- **Stop**: Stop playback and reset position to 0
- **Next**: Skip to next video in session
- **Previous**: Return to previous video in session

**Position Controls:**
- **Seek Forward**: Jump ahead by X seconds (configurable, default 5s)
- **Seek Backward**: Jump back by X seconds (configurable, default 5s)
- **Direct Seek**: Click on progress bar to jump to position

**Volume Controls:**
- **Volume Up**: Increase volume by 10% (Up Arrow)
- **Volume Down**: Decrease volume by 10% (Down Arrow)
- **Mute**: Toggle audio on/off

**Display Controls:**
- **Fullscreen**: Toggle fullscreen mode (F key)
- **Window Mode**: Windowed playback

### 1.3 Hardware Acceleration

**Windows Acceleration:**
- **Primary**: D3D11 (Direct3D 11) - Modern, efficient
- **Fallback**: DXVA (DirectX Video Acceleration)
- **Last Resort**: Software decode

**Configuration:**
```
VLC Arguments for Initialization:
  --vout=direct3d11        # Force D3D11 video output
  --sout-all               # All media to streaming output
  --network-caching=5000   # Network buffer (ms)
  --file-caching=5000      # File buffer (ms)
```

**Testing Matrix:**
| Format | Resolution | Codec | Hardware Accel | Result |
|--------|-----------|-------|----------------|--------|
| MP4 | 720p | H.264 | D3D11 | 60fps smooth |
| MKV | 1080p | H.264 | D3D11 | 60fps smooth |
| MP4 | 4K | H.265 | D3D11 | 60fps smooth |
| WebM | 1080p | VP9 | Software | 30fps(lower-end) |

## 2. Folder Playback & Shuffle

### 2.1 Recursive Folder Scanning

**Behavior:**
- User selects folder via UI or Explorer
- App recursively scans folder for video files
- Builds complete list including subfolders
- Counts total videos across all subdirectories
- Displays: "Total: 42 videos in 7 folders"

**Video Discovery:**
```
ScanForVideos(rootFolder):
  FOR EACH item IN Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories):
    IF item.Extension IN supportedExtensions:
      ADD VideoItem to list
  SORT by FullPath (alphabetical)
  RETURN list
```

**Supported Extensions:**
`.mp4 .mkv .avi .mov .wmv .webm .m4v .ts .m2ts .flv`

### 2.2 Sequential Mode

**Operation:**
- Videos play in alphabetical order by file path
- After video N, plays video N+1
- At end of playlist, stops or loops (configurable)
- UI shows: "Sequential mode"

**Example Order:**
```
Folder: C:\Videos
├─ Subfolder-A
│  ├─ video_01_a.mp4  (plays 1st)
│  └─ video_02_a.mp4  (plays 2nd)
└─ Subfolder-B
   ├─ video_01_b.avi  (plays 3rd)
   └─ video_02_b.mkv  (plays 4th)
```

### 2.3 Shuffle Mode with No-Repeat Guarantee

**Algorithm:**
```
InitializeShuffleMode():
  playedInCycle = empty HashSet
  allVideos = sorted list of all videos
  totalVideos = allVideos.Count

GetNextInShuffleMode():
  IF playedInCycle.Count == totalVideos:
    // Cycle complete
    EMIT CycleCompleted event
    PROMPT user: "Start new cycle?"
    IF user accepts:
      playedInCycle.Clear()
    ELSE:
      STOP playback
      RETURN null

  unplayedIndices = []
  FOR i = 0 TO totalVideos:
    IF i NOT IN playedInCycle:
      ADD i to unplayedIndices

  IF unplayedIndices.empty:
    RETURN null

  randomIndex = unplayedIndices[Random()]
  ADD randomIndex to playedInCycle
  RETURN allVideos[randomIndex]
```

**Guarantees:**
- ✅ No video repeats before all others played in cycle
- ✅ Random order (not sequential)
- ✅ Reproducible cycle detection
- ✅ Seamless transition between cycles

**Example (4 videos):**
```
Cycle 1: [Video 3] → [Video 1] → [Video 4] → [Video 2] → "Cycle complete"
Cycle 2: [Video 2] → [Video 4] → [Video 1] → [Video 3] → "Cycle complete"
```

### 2.4 Progress Display

**Real-time Counters:**
```
Mode: Sequential ▼
┌─────────────────┐
│ Total: 42       │
│ Played: 12      │
│ Remaining: 30   │
└─────────────────┘
```

**Updates:**
- Total: Constant (scanned once at folder open)
- Played: Increments when video marked as played
- Remaining: Total - Played (computed)
- Mode: Shows current playback mode

**Marking Video as Played:**
Occurs when:
1. Video reaches end naturally (PlaybackEnded event)
2. User marks manually (future feature)
3. 90% of video watched (time threshold)

## 3. Keyboard Shortcuts

### 3.1 Default Shortcuts

| Action | Shortcut | Customizable |
|--------|----------|-------------|
| **Playback** | | |
| Play/Pause | Space | ✅ Yes |
| Stop | ESC | ✅ Yes |
| Next | N | ✅ Yes |
| Previous | P | ✅ Yes |
| **Seek** | | |
| Forward (5s) | Right Arrow | ✅ Yes |
| Backward (5s) | Left Arrow | ✅ Yes |
| | | |
| **Volume** | | |
| Volume Up | Up Arrow | ✅ Yes |
| Volume Down | Down Arrow | ✅ Yes |
| Mute | M | ✅ Yes |
| **Display** | | |
| Fullscreen | F | ✅ Yes |
| **Capture** | | |
| Screenshot | Ctrl+S | ✅ Yes |
| **Move** | | |
| Move to Target 1 | Ctrl+1 | ✅ Yes |
| Move to Target 2 | Ctrl+2 | ✅ Yes |
| ... (3-8) | Ctrl+3 to Ctrl+8 | ✅ Yes |
| Move to Target 9 | Ctrl+9 | ✅ Yes |
| **UI** | | |
| Settings | Ctrl+, | ✅ Yes |
| Open File | Ctrl+O | ✅ Yes |
| Open Folder | Ctrl+Shift+O | ✅ Yes |

### 3.2 Customization

**Storage:** `%APPDATA%\PandaPlayer\settings.json`

```json
"KeyboardShortcuts": {
  "PlayPause": "Space",
  "Next": "N",
  "SeekForward": "Right"
}
```

**Future Phase:** UI dialog for editing shortcuts with conflict detection.

## 4. Screenshot Capture

### 4.1 Capture Behavior

**Trigger:** Ctrl+S (default, customizable)

**Capture Process:**
1. Pause video playback
2. Request current frame from VLC media engine
3. Encode frame as PNG image
4. Generate filename: `{video_name}_{timestamp}.png`
5. Save to configured screenshot folder
6. Resume playback
7. Show notification: "Screenshot saved to: path"

**Example Filename:**
```
Input: video_meeting_2026-02-24.mp4 at 14:30:45
Output: video_meeting_2026-02-24_20260224_143045.png
Path: C:\Users\YourUser\Pictures\PandaPlayer\video_meeting_2026-02-24_20260224_143045.png
```

### 4.2 Configuration

**Settings:**
```json
"ScreenshotFolder": "C:\\Users\\YourUser\\Pictures\\PandaPlayer"
```

**UI Configuration:**
1. Open Settings (Ctrl+,)
2. Find "Screenshot Folder" field
3. Click browse button
4. Select folder
5. Click Save

**Auto-creation:** If folder doesn't exist, app creates it on first screenshot.

## 5. Move-to-Folder Workflow

### 5.1 Target Configuration

**Setup:**
- Define up to 9 destination folders (Move Targets 1-9)
- Example use case:
  - Target 1: "C:\Videos\Approved" (reviewed OK)
  - Target 2: "C:\Videos\Needs-Edit" (flag for editing)
  - Target 3: "C:\Videos\Archive" (completed)

**Storage:**
```json
"MoveTargetFolders": {
  "1": "C:\\Videos\\Approved",
  "2": "C:\\Videos\\Needs-Edit",
  "3": "C:\\Videos\\Archive"
}
```

### 5.2 Move Trigger

**Methods:**
1. **Keyboard Shortcut:** Ctrl+1 through Ctrl+9 (while video playing)
2. **Right-click Context Menu:** Right-click on video → "Move to Target 1..." (future)
3. **UI Button:** Click move target button in Jobs panel

**Workflow:**
```
User presses Ctrl+1
  ↓
MainWindow KeyDown handler
  ↓
_fileMoveService.MoveFileAsync(currentVideo.FilePath, target1Path)
  ↓
FileMoveJob created and added to _activeJobs
  ↓
Safe move pipeline begins (background thread)
  ↓
PlaybackContinues uninterrupted
  ↓
UI shows job progress in Jobs panel
  ↓
On completion: source deleted, job moved to _completedJobs
```

### 5.3 Safe Move Pipeline Details

**Phase 1: Prepare**
- Input: sourceFile, destinationFolder
- Validate: Source exists, destination writable
- Lock: Release playback lock on source
- Check: Destination disk space > file size

**Phase 2: Copy**
- Create: tempFile = destFolder\filename.panda.partial
- Loop:
  - Read 1MB from source
  - Write to temp
  - Update progress: BytesCopied, Speed (MB/s), ETA
  - Allow user cancellation
- Until: EOF or cancelled

**Phase 3: Verify**
- Compare: File sizes match
- Checksum: SHA-256(source) == SHA-256(dest)
- Result: PASS → continue, FAIL → delete temp and report

**Phase 4: Conflict Check**
- Query: Does destFolder\filename exist?
- If YES:
  - Emit: JobConflict event
  - Show: Dialog to user with options
  - Options: Rename | Overwrite | Skip
  - Wait: User selection

**Phase 5: Finalize**
- Rename: tempFile → finalFile
- Verify: Rename succeeded
- If FAIL: Report conflict option failed

**Phase 6: Source Deletion**
- Delete: source file
- Only execute if phases 1-5 all succeeded
- If DELETE fails: Log warning (destination safe)

**Phase 7: Complete**
- Status: Completed
- Move job to _completedJobs
- Emit: JobCompleted event
- Release: Semaphore slot

### 5.4 Progress Tracking

**UI Display (Jobs Panel):**
```
[X] video_interview.mp4
    ████████████░░░░░░░░ 65%
    2.5 MB/s | ETA 0:45 | CANCEL
```

**Real-time Updates:**
- Progress percentage (0-100%)
- Transfer speed (MB/s)
- Estimated time remaining
- Cancel button for current job

**Data Model:**
```csharp
FileMoveJob
{
  JobId: "job-123",
  SourceFilePath: "C:\\source\\video.mp4",
  DestinationFolderPath: "C:\\target",
  Status: FileMoveStatus.Copying,
  ProgressPercentage: 65.5,
  BytesCopied: 652857344,
  TotalBytes: 1000000000,
  SpeedMbps: 2.5,
  EstimatedTimeRemaining: TimeSpan.FromSeconds(140)
}
```

### 5.5 Conflict Resolution

**Scenarios:**
1. Destination file already exists
2. Destination folder running low on space
3. Source file becomes unavailable

**Resolution Dialog:**
```
┌─────────────────────────────────┐
│ Move Conflict Detected           │
├─────────────────────────────────┤
│ File exists: video.mp4           │
│ Last modified: 2026-02-20        │
├─────────────────────────────────┤
│ [Rename] [Overwrite] [Skip]      │
└─────────────────────────────────┘
```

**Options:**
- **Rename:** Auto-append timestamp (video_20260224_143045.mp4)
- **Overwrite:** Replace existing file (use with caution)
- **Skip:** Cancel move, keep source intact

## 6. Session Persistence

### 6.1 Automatic Session Restore

**On App Startup:**
```
AppWindow.Loaded event
  ↓
LoadLastSessionAsync()
  ↓
Query: Session files in sessions\ folder
  ↓
Find: Most recent by CreatedAt timestamp
  ↓
Load: PlaybackSession from JSON
  ↓
RestorePlaylist()
    Restore CurrentVideoIndex
    Restore PlaybackMode (Sequential/Shuffle)
    Restore Played state per video
  ↓
Display: "Session restored: C:\Videos (12/42 played)"
```

### 6.2 Progress Persistence

**OnVideoCompleted Event:**
```
PlaybackEnded event → SessionService
  ↓
MarkVideoAsPlayedAsync(video)
  ├─ Set: IsPlayed = true
  ├─ Set: LastPlayedAt = DateTime.Now
  ├─ Increment: TotalVideosPlayed
  └─ Update: Session.LastModifiedAt
  ↓
StateStore.SaveSessionAsync(session)
  ├─ Generate: Session hash
  ├─ Write: session_{hash}.json
  └─ Persist: All playback metadata
```

### 6.3 Manual Reset

**User Action:**
```
UI Button: "Reset Progress" clicked
  ↓
ShowConfirmation Dialog
  ├─ "Reset playback progress for this session?"
  ├─ "This action cannot be undone."
  └─ [Reset] [Cancel]
  ↓
User accepts: Reset
  ↓
SessionService.ResetProgressAsync()
  ├─ Loop through AllVideos:
  │  ├─ Set: IsPlayed = false
  │  └─ Set: LastPlayedAt = DateTime.MinValue
  ├─ Set: TotalVideosPlayed = 0
  └─ Clear: Played state HashSet (for shuffle)
  ↓
StateStore.SaveSessionAsync(session)
  ↓
UI Update: Progress counters reset
```

## 7. Windows Explorer Integration

### 7.1 Context Menu Entries

**Video Files:**
- **Extensions:** .mp4, .mkv, .avi, .mov, .wmv, .webm, .m4v, .ts, .m2ts, .flv
- **Menu Item:** "Play with Panda Player"
- **Action:** Launch app with file path as argument

**Folders:**
- **All Folders:** Any directory
- **Menu Item:** "Play Folder with Panda Player"
- **Action:** Launch app with folder path as argument

### 7.2 Registry Integration

**Entry Point:**
```batch
# Video files
HKCR\.mp4\shell\PandaPlayer
  (Default) = "Play with Panda Player"
  HKCR\.mp4\shell\PandaPlayer\command
    (Default) = "C:\path\to\PandaPlayer.exe "%1""

# Folders
HKCR\Directory\shell\PandaPlayerFolder
  (Default) = "Play Folder with Panda Player"
  HKCR\Directory\shell\PandaPlayerFolder\command
    (Default) = "C:\path\to\PandaPlayer.exe "%1""
```

### 7.3 Launch Argument Handling

**Program Entry Point:**
```csharp
[STAThread]
static void Main(string[] args)
{
  if (args.Length > 0)
  {
    string path = args[0];
    if (File.Exists(path))
      // Open video file
      app.Resources["LaunchPath"] = path;
      app.Resources["LaunchMode"] = "file";
    else if (Directory.Exists(path))
      // Open folder
      app.Resources["LaunchPath"] = path;
      app.Resources["LaunchMode"] = "folder";
  }
  app.Run();
}
```

## 8. Settings & Configuration

### 8.1 User Settings

**Location:** `%APPDATA%\PandaPlayer\settings.json`

**Full Schema:**
```json
{
  "SeekForwardSeconds": 5,
  "SeekBackwardSeconds": 5,
  "Volume": 1.0,
  "HardwareAccelerationEnabled": true,
  "ScreenshotFolder": "C:\\Users\\User\\Pictures\\PandaPlayer",
  "MoveTargetFolders": {
    "1": "C:\\Videos\\Approved",
    "2": "C:\\Videos\\Needs-Edit"
  },
  "DefaultPlaybackMode": "Sequential",
  "KeyboardShortcuts": {
    "PlayPause": "Space",
    "Next": "N",
    "SeekForward": "Right"
  },
  "MaxConcurrentMoves": 2,
  "VerifyChecksum": true,
  "DeleteSourceAfterVerification": true
}
```

### 8.2 Session Metadata

**Location:** `%APPDATA%\PandaPlayer\sessions\session_{hash}.json`

**Schema:**
```json
{
  "SessionId": "guid",
  "RootFolderPath": "C:\\Videos",
  "AllVideos": [
    {
      "FilePath": "C:\\Videos\\video1.mp4",
      "FileName": "video1.mp4",
      "FolderPath": "C:\\Videos",
      "FileSizeBytes": 5368709120,
      "Duration": "PT1H23M45S",
      "IsPlayed": true,
      "LastPlayedAt": "2026-02-24T14:30:00Z",
      "PlaybackIndex": 0
    }
  ],
  "CurrentVideoIndex": 5,
  "Mode": "Sequential",
  "TotalVideosPlayed": 12,
  "CreatedAt": "2026-02-20T10:00:00Z",
  "LastModifiedAt": "2026-02-24T14:35:00Z"
}
```

---

Document Version: 1.0  
Last Updated: 2026-02-24
