@echo off
rem Builds bin\MuteMyMic.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No Visual Studio or SDK needed.
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo C# compiler not found. Install .NET Framework 4.8.
  exit /b 1
)

if not exist bin mkdir bin

if not exist assets\app.ico (
  if not exist assets mkdir assets
  "%CSC%" /nologo /codepage:65001 /target:exe /out:bin\MakeIcon.exe /r:System.Drawing.dll tools\MakeIcon.cs src\IconPainter.cs || exit /b 1
  bin\MakeIcon.exe assets\app.ico || exit /b 1
  del bin\MakeIcon.exe
)

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /out:bin\MuteMyMic.exe ^
  /win32icon:assets\app.ico /win32manifest:src\app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\*.cs || exit /b 1

echo.
echo Done: bin\MuteMyMic.exe
