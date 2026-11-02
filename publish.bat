@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ============================================
echo  Сборка backend API в один .exe...
echo ============================================
dotnet publish UrlShortener.Api -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o publish\Api

echo.
echo ============================================
echo  Сборка WPF-админки в один .exe...
echo ============================================
dotnet publish UrlShortener.Admin -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o publish\Admin

echo.
echo ============================================
echo  Готово!
echo  API:     publish\Api\UrlShortener.Api.exe
echo  Админка: publish\Admin\UrlShortener.Admin.exe
echo ============================================
pause
