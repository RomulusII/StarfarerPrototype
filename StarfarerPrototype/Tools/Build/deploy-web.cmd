@echo off
rem ---------------------------------------------------------------------------
rem Build + yukleme, arka arkaya. Tek komut.
rem
rem   Tools\Build\deploy-web.cmd       -> build-web.cmd, sonra upload-web.cmd
rem   Tools\Build\deploy-web.cmd apk   -> APK'yi da yeniden uretir
rem
rem Build duserse yukleme YAPILMAZ: yarim ya da eski bir Builds\Web klasorunu
rem yeni surum sanip yayina almak, testcinin yanlis paketi oynamasi demek.
rem ---------------------------------------------------------------------------
setlocal

call "%~dp0build-web.cmd" %1
if errorlevel 1 (
  echo [BASARISIZ] Build duser, yukleme yapilmadi.
  exit /b 1
)

echo.
call "%~dp0upload-web.cmd"
if errorlevel 1 exit /b 1

echo.
echo [TAMAM] Build + dagitim bitti.
exit /b 0
