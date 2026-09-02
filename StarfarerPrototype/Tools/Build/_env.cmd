@echo off
rem ---------------------------------------------------------------------------
rem Ortak ortam kurulumu. Dogrudan calistirilmaz; digerleri "call" eder.
rem Neden gerekli oldugu Tools/Build/README.md'de yazili.
rem ---------------------------------------------------------------------------

rem Proje koku: bu dosya Tools\Build\ altinda, yani iki ust dizin.
for %%i in ("%~dp0..\..") do set "PROJ=%%~fi"

rem Unity surumunu projenin kendisinden oku - Unity yukseltilince script bozulmasin.
set "UNITY_VER="
for /f "tokens=2" %%v in ('findstr /b "m_EditorVersion:" "%PROJ%\ProjectSettings\ProjectVersion.txt"') do set "UNITY_VER=%%v"
if not defined UNITY_VER (
  echo [HATA] ProjectVersion.txt okunamadi: %PROJ%\ProjectSettings\ProjectVersion.txt
  exit /b 1
)

rem UNITY_EXE disaridan verilebilir; verilmezse Hub'in standart yolu.
if not defined UNITY_EXE set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\%UNITY_VER%\Editor\Unity.exe"
if not exist "%UNITY_EXE%" (
  echo [HATA] Unity bulunamadi: %UNITY_EXE%
  echo        UNITY_EXE ortam degiskeniyle elle gosterebilirsin.
  exit /b 1
)

rem --- ASIL DUZELTME --------------------------------------------------------
rem Gradle bu makinede "Unable to establish loopback connection" ile duser.
rem Sebep AppData\Local\Temp altinda AF_UNIX socket acilamamasi; ayrinti README.
rem TEMP/TMP'yi baska bir dizine almak yeter, JVM bayragi gerekmez.
if not exist "%USERPROFILE%\jtmp" mkdir "%USERPROFILE%\jtmp"
set "TEMP=%USERPROFILE%\jtmp"
set "TMP=%USERPROFILE%\jtmp"

exit /b 0
