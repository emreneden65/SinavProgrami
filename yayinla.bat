@echo off
REM ============================================================
REM  SINAV PROGRAMI - Yayinlama baslaticisi
REM  Bu dosyaya cift tiklayin. Asil islemi yayinla.ps1 yapar.
REM ============================================================
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0yayinla.ps1"
pause
