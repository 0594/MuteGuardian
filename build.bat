@echo off
rem ===========================================================
rem  MuteGuardian build script
rem  Display name inside the program is Chinese (Jing Yin Shi Wei)
rem  Requires only the .NET Framework 4.x compiler shipped with
rem  Windows. No SDK / runtime installation needed.
rem ===========================================================
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found - .NET Framework 4.x is required.
  pause
  exit /b 1
)

echo [1/2] generating icon ...
"%CSC%" /nologo /codepage:65001 /target:exe /out:"tools\IcoGen.exe" /r:System.Drawing.dll "tools\IcoGen.cs"
if errorlevel 1 goto fail
"tools\IcoGen.exe" "MuteGuardian.ico"
if errorlevel 1 goto fail

echo [2/2] compiling MuteGuardian.exe ...
"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ ^
  /win32icon:"MuteGuardian.ico" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /out:"MuteGuardian.exe" "MuteGuardian.cs"
if errorlevel 1 goto fail

echo.
echo Done: MuteGuardian.exe
pause
exit /b 0

:fail
echo.
echo [ERROR] build failed.
pause
exit /b 1
