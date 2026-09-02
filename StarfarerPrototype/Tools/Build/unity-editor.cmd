@echo off
rem ---------------------------------------------------------------------------
rem Unity Editor'u projeyle birlikte, Gradle duzeltmesi UYGULANMIS halde acar.
rem
rem Editor'u Hub'dan ya da masaustu kisayolundan acarsan Android Build & Run
rem "Unable to establish loopback connection" ile duser. Sebep Editor'de degil,
rem Editor'un devraldigi TEMP degiskeninde; ayrinti README.md'de.
rem
rem Kisayol yapmak istersen: bu dosyaya sag tik > Kisayol olustur.
rem ---------------------------------------------------------------------------
setlocal

call "%~dp0_env.cmd" || exit /b 1

echo Unity : %UNITY_EXE%
echo Proje : %PROJ%
echo TEMP  : %TEMP%
echo.
echo Editor aciliyor...

start "" "%UNITY_EXE%" -projectPath "%PROJ%"
exit /b 0
