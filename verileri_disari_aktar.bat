@echo off
REM ============================================================
REM  SINAV PROGRAMI - Eski SQL Server verisini disari aktar
REM  Bu dosyaya cift tiklayin. Asil islemi
REM  verileri_disari_aktar.ps1 yapar.
REM ============================================================
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0verileri_disari_aktar.ps1"
pause
