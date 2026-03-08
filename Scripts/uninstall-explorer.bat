@echo off
REM Codex Player - Explorer Integration Cleanup
REM Removes context menu entries

echo ========================================
echo Codex Player - Uninstall Explorer Integration
echo ========================================

echo.
echo Removing Explorer context menu entries...

REM Remove context menu entries for each video extension
set VIDEO_EXTENSIONS=mp4 mkv avi mov wmv webm m4v ts m2ts flv

for %%E in (!VIDEO_EXTENSIONS!) do (
    reg delete "HKCR\.%%E\shell\CodexPlayer" /f >nul 2>&1
    echo  - Removed .%%E entry
)

REM Remove folder context menu
reg delete "HKCR\Directory\shell\CodexPlayerFolder" /f >nul 2>&1
echo  - Removed folder entry

echo.
echo ========================================
echo Explorer integration removed!
echo ========================================

pause
