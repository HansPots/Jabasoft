# Jabasoft

WPF-shell voor de hele JabaSoft-familie. Vanuit hier open je de andere apps
(`JabaSoft.TabStudio`, `JabaSoft.LocalAiStudio`) en zie je het gezamenlijke
token-verbruik.

## Architectuur

Het hele venster is één `WebView2`-control (`Jabasoft.App/MainWindow.xaml`).
De shell-chrome (header, menu, content-area) is gewone HTML/CSS/JS onder
`Jabasoft.App/Assets/Shell/`, niet native XAML — zo kan de shell dezelfde
`jabasoft-theme.css` gebruiken als de andere apps. De ingesloten apps
(TabStudio, LocalAiStudio) worden getoond via een `<iframe>` in die pagina;
er zijn geen aparte native WebView2-instances per app nodig.

Twee virtual-host-mappings (`CoreWebView2.SetVirtualHostNameToFolderMapping`,
ingesteld in `MainWindow.xaml.cs`) laten de shell lokale bestanden laden
zonder een eigen webserver:

- `https://app.jabasoft.local/...` → `Assets/Shell/` (deze app zelf, vanuit
  de build-output-map — `config.js` wordt hier bij elke start opnieuw
  gegenereerd, dus dat bestand hoort niet in source control).
- `https://shared.jabasoft.local/...` → `Jabasoft.Stylebook/Shared.UI/wwwroot/`
  (rechtstreeks vanaf schijf, hetzelfde fysieke bestand als TabStudio en
  LocalAiStudio via `_content/Shared.UI/...` laden — zie
  `Jabasoft.Stylebook/README.md`).

Voor het token-verbruik-dashboard host de app zelf een kleine, in-process
ASP.NET Core minimal API (`http://localhost:5300` standaard, instelbaar via
`Api:BaseUrl` in `appsettings.json`) die rechtstreeks op de gedeelde
`JabaSoftTelemetry`-database leest (via `Shared.Telemetry`, projectverwijzing
naar `Jabasoft.Stylebook`). `Assets/Shell/dashboard.html` haalt die API op.

## Configuratie (`Jabasoft.App/appsettings.json`)

- `ConnectionStrings:JabaSoftTelemetry` — zelfde connection string als
  TabStudio/LocalAiStudio.
- `SharedUi:ThemeFolder` — absoluut pad naar `Jabasoft.Stylebook/Shared.UI/wwwroot`.
  Standaard uitgegaan van `C:\Repos\Jabasoft.Stylebook\Shared.UI\wwwroot`.
- `Apps:<Naam>:DevelopmentUrl` / `Apps:<Naam>:MainUrl` — waar de betreffende
  app te bereiken is. `MainUrl` is nu leeg (nog geen IIS-hosting van de
  main-branch ingericht); zodra dat er is, hier invullen — geen codewijziging
  nodig. Zolang `MainUrl` leeg is, wordt `DevelopmentUrl` gebruikt.
- `Apps:<Naam>:ProjectPath` — map van de app's web-project (bv.
  `TabStudio.Web`), gebruikt om die app automatisch te starten (zie hieronder).

## Apps automatisch starten

Bij het opstarten controleert `MainWindow.xaml.cs` (`EnsureAppsRunningAsync`)
per geconfigureerde app of `DevelopmentUrl` al bereikbaar is. Zo niet, start
het zelf `dotnet run` in `ProjectPath` (met `ASPNETCORE_ENVIRONMENT=Development`)
en wacht tot de app reageert (max. 45s) voordat de shell navigeert. Draait een
app al (handmatig gestart, of van een vorige Jabasoft-sessie), dan blijft die
met rust. Bij het sluiten van Jabasoft worden alleen de apps die het zelf
startte weer afgesloten (`OnClosed`, met de hele procesboom). Een
"JABASOFT WORDT GESTART..."-scherm (`Assets/Shell/loading.html`) overbrugt de
wachttijd van een koude `dotnet run`-build.

## Starten

```bash
dotnet run --project Jabasoft.App
```

Of vanuit Visual Studio: `Jabasoft.slnx` openen en op F5/Start drukken —
`Jabasoft.App` is het enige project in de solution, dus dat wordt automatisch
het opstartproject. Het gedrag is identiek aan `dotnet run`: dezelfde
`OnLoaded`-logica start TabStudio/LocalAiStudio zo nodig zelf op.

Vereist dat SQL Server lokaal bereikbaar is voor de `JabaSoftTelemetry`-
database. TabStudio/LocalAiStudio hoeven niet meer los gestart te worden —
zie hierboven — maar dat kan nog steeds (dan gebruikt Jabasoft die instance).
