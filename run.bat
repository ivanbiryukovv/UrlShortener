@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo Запуск backend API...
start "UrlShortener API" cmd /k dotnet run --project UrlShortener.Api

echo Ожидание старта API (8 сек)...
timeout /t 8 /nobreak >nul

echo Запуск WPF-админки...
start "UrlShortener Admin" cmd /k dotnet run --project UrlShortener.Admin


