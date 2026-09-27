@echo off
rem 用 Windows 自带的 C# 编译器构建桌宠（无需安装 .NET SDK）
setlocal
cd /d "%~dp0"

set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo 找不到 csc.exe，请确认已安装 .NET Framework 4.x
  exit /b 1
)

echo 使用编译器: %CSC%
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:鲸鱼娘桌宠.exe ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  Pet.cs

if errorlevel 1 (
  echo.
  echo 编译失败
  exit /b 1
)
echo.
echo 编译成功: 鲸鱼娘桌宠.exe
echo 可运行 鲸鱼娘桌宠.exe --selftest 做一次自检
endlocal
