@echo off
rem Build the desktop pet with the C# compiler bundled in Windows.
rem No .NET SDK required.
rem NOTE: this file is saved as ANSI/GBK on purpose. cmd.exe cannot read a
rem UTF-8 .bat/.cmd correctly, which garbles non-ASCII output paths.
setlocal
cd /d "%~dp0"

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [build] csc.exe not found. Install .NET Framework 4.x first.
  exit /b 1
)

echo [build] compiler: %CSC%
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:¾¨ÓãÄï×À³è.exe ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  Pet.cs

if errorlevel 1 (
  echo.
  echo [build] FAILED
  exit /b 1
)

echo.
echo [build] OK -^> ¾¨ÓãÄï×À³è.exe
echo [build] run "¾¨ÓãÄï×À³è.exe --selftest" to verify
endlocal
