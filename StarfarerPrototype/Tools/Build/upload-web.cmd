@echo off
rem ---------------------------------------------------------------------------
rem Builds\Web klasorunu sunucuya YUKLER ve yayina alir. Build almaz
rem (bkz. build-web.cmd).
rem
rem   Tools\Build\upload-web.cmd            -> yukle + yayina al
rem   Tools\Build\upload-web.cmd noswap     -> yukle, yayina ALMA
rem   Tools\Build\upload-web.cmd swaponly   -> onceden yuklenmisi yayina al
rem
rem Yapilandirma ve token: Tools\Deploy\deploy.config.json (repoya girmez).
rem Akis ve hata tablosu: Tools\Deploy\README.md
rem ---------------------------------------------------------------------------
setlocal

rem Proje koku: bu dosya Tools\Build\ altinda, yani iki ust dizin.
rem _env.cmd cagrilmaz - Unity gerekmiyor, yalnizca node.
for %%i in ("%~dp0..\..") do set "PROJ=%%~fi"

where node >nul 2>&1 || (
  echo [HATA] node bulunamadi. Node.js kurulu ve PATH'te olmali.
  exit /b 1
)

set "ARGS="
if /i "%~1"=="noswap"   set "ARGS=--no-swap"
if /i "%~1"=="swaponly" set "ARGS=--swap-only"

set "BUILDNO=?"
if exist "%PROJ%\Tools\Deploy\build-number.txt" set /p BUILDNO=<"%PROJ%\Tools\Deploy\build-number.txt"

echo [upload] Sunucuya yukleniyor (son build numarasi %BUILDNO%)...
node "%PROJ%\Tools\Deploy\upload.js" %ARGS%
if errorlevel 1 (
  echo [BASARISIZ] Yukleme duser. Build yerinde: %PROJ%\Builds\Web
  echo             Tekrar denemek icin: Tools\Build\upload-web.cmd
  exit /b 1
)

echo [TAMAM] Yukleme bitti.
exit /b 0
