@echo off
rem ---------------------------------------------------------------------------
rem Editor KAPALIYKEN komut satirindan Android APK uretir.
rem
rem   Tools\Build\build-android.cmd            -> release  (Builds\Android\Starfarer.apk)
rem   Tools\Build\build-android.cmd dev        -> development build (logcat'te yigin izi)
rem
rem Cikti ve log yollari sonda yazdirilir.
rem ---------------------------------------------------------------------------
setlocal

call "%~dp0_env.cmd" || exit /b 1

set "METHOD=AndroidBuild.Apk"
if /i "%~1"=="dev" set "METHOD=AndroidBuild.ApkDev"

set "LOG=%PROJ%\Logs\build-android.log"
if not exist "%PROJ%\Logs" mkdir "%PROJ%\Logs"

echo Unity   : %UNITY_EXE%
echo Proje   : %PROJ%
echo Metot   : %METHOD%
echo TEMP    : %TEMP%
echo Log     : %LOG%
echo.
echo Build basladi. Ilk Android gecisi tum asset'leri yeniden import eder, uzun surer...

"%UNITY_EXE%" -batchmode -nographics ^
  -projectPath "%PROJ%" ^
  -buildTarget Android ^
  -executeMethod %METHOD% ^
  -logFile "%LOG%"

set "CODE=%ERRORLEVEL%"
echo.
findstr /c:"[AndroidBuild]" "%LOG%"
echo.
if not "%CODE%"=="0" (
  echo [BASARISIZ] Unity cikis kodu %CODE%. Ayrinti: %LOG%
  exit /b %CODE%
)
echo [TAMAM] Cikti: %PROJ%\Builds\Android\
exit /b 0
