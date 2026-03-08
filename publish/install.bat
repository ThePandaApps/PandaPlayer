@echo off
SET "APP_NAME=Panda Player"
SET "EXE_NAME=Panda Player.UI.exe"
SET "INSTALL_DIR=%LOCALAPPDATA%\Panda Player\Bin"

echo Installing %APP_NAME%...
mkdir "%INSTALL_DIR%"
xcopy /E /I /Y "." "%INSTALL_DIR%"

echo Registering Context Menu...
reg add "HKCU\Software\Classes\Directory\shell\Panda Player" /ve /d "Play with Panda Player" /f
reg add "HKCU\Software\Classes\Directory\shell\Panda Player" /v "Icon" /d "\"%INSTALL_DIR%\%EXE_NAME%\"" /f
reg add "HKCU\Software\Classes\Directory\shell\Panda Player\command" /ve /d "\"%INSTALL_DIR%\%EXE_NAME%\" \"%%1\"" /f

echo.
echo Installation Complete!
echo You can now right-click any folder and select 'Play with Panda Player'.
pause
