@echo off
rem CIRLAT build: testovi, pa jedan self-contained Cirlat.exe. Potrebno: .NET 10 SDK.
setlocal
cd /d "%~dp0"
echo == Testovi ==
dotnet run --project tests\Cirlat.Tests -c Release
if errorlevel 1 (
  echo.
  echo TESTOVI NISU PROSLI.
  exit /b 1
)
echo.
echo == Publish ==
dotnet publish src\Cirlat\Cirlat.csproj -c Release -o publish
if errorlevel 1 (
  echo.
  echo BUILD NIJE USPEO.
  exit /b 1
)
echo.
echo Gotovo: %~dp0publish\Cirlat.exe
endlocal
