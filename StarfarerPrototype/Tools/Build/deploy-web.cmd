@echo off
rem ---------------------------------------------------------------------------
rem Tarayici build'i alir ve sunucuya dagitir. Tek komut.
rem
rem   Tools\Build\deploy-web.cmd           -> web build + dagitim
rem   Tools\Build\deploy-web.cmd apk       -> once APK'yi da yeniden uretir
rem   Tools\Build\deploy-web.cmd noupload  -> yalnizca build, dagitim yok
rem
rem APK NEDEN HER SEFERINDE URETILMIYOR: Android'e gecis butun asset'leri
rem yeniden import eder ve web dagitimini dakikalarca uzatir. Sayfadaki APK
rem linki icin Builds\Android\Starfarer.apk VARSA kopyalanir; yoksa link
rem sayfada zaten gorunmez (index.html HEAD yoklamasi yapiyor).
rem
rem BUILD NUMARASI her calistirmada artar ve -sfBuild ile pakete islenir.
rem Sabit surumle dagitmak, testcinin tarayicisinin eski paketi vermesi
rem demekti; ayrinti BuildStamp.cs icinde.
rem ---------------------------------------------------------------------------
setlocal

call "%~dp0_env.cmd" || exit /b 1

set "APK=0"
set "UPLOAD=1"
if /i "%~1"=="apk"      set "APK=1"
if /i "%~1"=="noupload" set "UPLOAD=0"

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
  echo [1/3] Android paketi uretiliyor... ilk gecis uzun surer.
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
echo [2/3] Web build'i uretiliyor (surum 1.0.0+b%BUILDNO%)...
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

if "%UPLOAD%"=="0" (
  echo [TAMAM] Build hazir, dagitim yapilmadi: %PROJ%\Builds\Web
  exit /b 0
)

rem --- 3) Dagitim -----------------------------------------------------------
echo [3/3] Sunucuya yukleniyor...
node "%PROJ%\Tools\Deploy\upload.js"
if errorlevel 1 (
  echo [BASARISIZ] Yukleme duser. Build yerinde: %PROJ%\Builds\Web
  echo             Tekrar denemek icin: node Tools\Deploy\upload.js
  exit /b 1
)

echo.
echo [TAMAM] Surum 1.0.0+b%BUILDNO% yayinda.
exit /b 0
