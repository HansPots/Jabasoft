@echo off
rem Bouwt eerst de hele Jabasoft-solution (incrementeel: een paar seconden als er niets veranderd is)
rem en start daarna de applicatie. Mislukt het bouwen, dan blijft dit venster open met de fouten
rem en wordt Jabasoft niet gestart.

rem Naast dit script, in de Jabasoft-repo - dus het werkt ook als de repo's ergens anders staan.
set "SOLUTION=%~dp0Jabasoft.slnx"
set "APP=%~dp0Jabasoft.App\bin\Debug\net10.0-windows\Jabasoft.App.exe"

tasklist /FI "IMAGENAME eq Jabasoft.App.exe" 2>nul | find /I "Jabasoft.App.exe" >nul
if not errorlevel 1 (
    echo Jabasoft draait al - sluit die eerst als je de nieuwste code wilt starten.
    timeout /t 4 >nul
    exit /b 0
)

rem Oude achtergronddiensten opruimen voor we gaan bouwen: de AI-broker houdt bestanden van de gedeelde bibliotheek
rem vast en laat het bouwen anders mislukken. Alleen als er geen app van de familie draait - die heeft ze misschien nodig.
rem (Jabasoft zelf draait hier niet: dat is hierboven al afgehandeld.)
powershell -NoProfile -Command "$apps='Stylebook.Playground'; if(-not (Get-Process -Name $apps -ErrorAction SilentlyContinue)){ foreach($poort in 5310,5320,5330){ Get-NetTCPConnection -LocalPort $poort -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue } }; Get-Process -Name 'Jabasoft.Broker' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }"

title Jabasoft - bouwen...
echo Jabasoft bouwen...
dotnet build "%SOLUTION%" --nologo -v quiet
if errorlevel 1 (
    echo.
    echo Bouwen mislukt - Jabasoft wordt niet gestart.
    pause
    exit /b 1
)

start "" "%APP%"
