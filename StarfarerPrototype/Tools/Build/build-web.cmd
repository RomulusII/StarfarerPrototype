@echo off
rem ---------------------------------------------------------------------------
rem Tarayici build'ini URETIR. Yukleme yapmaz (bkz. upload-web.cmd).
rem
rem   Tools\Build\build-web.cmd       -> web build  (Builds\Web)
rem   Tools\Build\build-web.cmd apk   -> once APK'yi da yeniden uretir
rem
rem Editor KAPALI olmali: Unity ayni projeyi iki kez acamaz.
rem
rem APK NEDEN HER SEFERINDE URETILMIYOR: Android'e gecis butun asset'leri
rem yeniden import eder ve build'i dakikalarca uzatir. Sayfadaki APK linki
rem icin Builds\Android\Starfarer.apk VARSA kopyalanir; yoksa link sayfada
rem zaten gorunmez (index.html HEAD yoklamasi yapiyor).
rem
rem BUILD NUMARASI her calistirmada artar ve -sfBuild ile pakete islenir.
rem Sabit surumle dagitmak, testcinin tarayicisinin eski paketi vermesi
rem demekti; ayrinti BuildStamp.cs icinde.
rem ---------------------------------------------------------------------------
setlocal

call "%~dp0_env.cmd" || exit /b 1

set "APK=0"
if /i "%~1"=="apk" set "APK=1"

rem --- Build numarasi -------------------------------------------------------
set "NUMFILE=%PROJ%\Tools\Deploy\build-number.txt"
set "BUILDNO=0"
if exist "%NUMFILE%" set /p BUILDNO=<"%NUMFILE%"
set /a BUILDNO=BUILDNO+1
if not exist "%PROJ%\Tools\Deploy" mkdir "%PROJ%\Tools\Deploy"
> "%NUMFILE%" echo %BUILDNO%

if not exist "%PROJ%\Logs" mkdir "%PROJ%\Logs"

rem --- 1) Android paketi (istege bagli) -------------------------------------
if "%APK%"=="1" (
  echo [build] Android paketi uretiliyor... ilk gecis uzun surer.
  "%UNITY_EXE%" -batchmode -nographics ^
    -projectPath "%PROJ%" ^
    -buildTarget Android ^
    -executeMethod AndroidBuild.Apk ^
    -sfBuild %BUILDNO% ^
    -logFile "%PROJ%\Logs\build-android.log"
  if errorlevel 1 (
    echo [BASARISIZ] Android build'i duser. Ayrinti: %PROJ%\Logs\build-android.log
    exit /b 1
  )
  findstr /c:"[AndroidBuild]" "%PROJ%\Logs\build-android.log"
)

rem --- 2) Web build ---------------------------------------------------------
set "WLOG=%PROJ%\Logs\build-web.log"
echo [build] Web build'i uretiliyor (build %BUILDNO%)...
"%UNITY_EXE%" -batchmode -nographics ^
  -projectPath "%PROJ%" ^
  -buildTarget WebGL ^
  -executeMethod WebBuild.Web ^
  -sfBuild %BUILDNO% ^
  -logFile "%WLOG%"

set "CODE=%ERRORLEVEL%"
findstr /c:"[WebBuild]" "%WLOG%"
findstr /c:"[BuildStamp]" "%WLOG%"
if not "%CODE%"=="0" (
  echo [BASARISIZ] Unity cikis kodu %CODE%. Ayrinti: %WLOG%
  echo             Editor acik mi? Acikken batchmode build proje kilidine takilir.
  exit /b %CODE%
)

rem Paket oyunla AYNI klasorde durur: IIS yapilandirmasi ust klasore dogru
rem islemiyor, sablonun web.config'i .apk eslemesini yalnizca kendi klasoru
rem icin veriyor. Ayrinti index.html icindeki APK notunda.
if exist "%PROJ%\Builds\Android\Starfarer.apk" (
  copy /y "%PROJ%\Builds\Android\Starfarer.apk" "%PROJ%\Builds\Web\Starfarer.apk" >nul
  echo APK kopyalandi: Builds\Web\Starfarer.apk
) else (
  echo APK yok, atlandi ^(sayfadaki link gorunmeyecek^)
)

echo [TAMAM] Build %BUILDNO% hazir: %PROJ%\Builds\Web
exit /b 0
