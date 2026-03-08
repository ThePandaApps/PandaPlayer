@echo off
REM Panda Player - Explorer Integration Setup
REM Registers context menu entries for video files and folders

echo ========================================
echo Panda Player - Explorer Integration Setup
echo ========================================

setlocal enabledelayedexpansion

REM Get the executable path
set CODEX_EXE=%1
if "!CODEX_EXE!"=="" (
    echo Usage: setup-explorer.bat "path\to\Panda Player.exe"
    exit /b 1
)

if not exist "!CODEX_EXE!" (
    echo Error: Executable not found: !CODEX_EXE!
    exit /b 1
)

echo.
echo Installing Explorer context menu for video files...

REM Video file extensions
set VIDEO_EXTENSIONS=mp4 mkv avi mov wmv webm m4v ts m2ts flv

REM Create context menu entries for each video extension
for %%E in (!VIDEO_EXTENSIONS!) do (
    reg add "HKCR\.%%E\shell\Panda Player" /ve /d "Play with Panda Player" /f >nul
    reg add "HKCR\.%%E\shell\Panda Player\command" /ve /d "\"!CODEX_EXE!\" \"%%1\"" /f >nul
    echo  - Registered .%%E
)

echo.
echo Installing folder context menu...

REM Add context menu for folders
reg add "HKCR\Directory\shell\Panda PlayerFolder" /ve /d "Play Folder with Panda Player" /f >nul
reg add "HKCR\Directory\shell\Panda PlayerFolder\command" /ve /d "\"!CODEX_EXE!\" \"%%1\"" /f >nul

echo.
echo ========================================
echo Explorer integration completed!
echo ========================================
echo.
echo Right-click menu options are now available for:
echo - Video files (.mp4, .mkv, .avi, .mov, .wmv, .webm, .m4v, .ts, .m2ts, .flv)
echo - Folders
echo.

pause
