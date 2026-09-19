using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Jabasoft.App.Controls;
using Jabasoft.Base;
using Jabasoft.Base.AiBroker;
using Jabasoft.Base.Health;
using Jabasoft.Base.Logging;
using InhoudSchermen = Jabasoft.App.Regios.Inhoud;

namespace Jabasoft.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>De naam in appsettings.json van de app die de Stylebook-knop start.</summary>
    private const string StylebookAppName = "Stylebook";

    /// <summary>De naam in appsettings.json van de app die de AI Studio-knop start.</summary>
    private const string AiStudioAppName = "LocalAiStudio";

    /// <summary>Gestarte processen per AppEntry.Name - zie StartOrFocusApp. Geen HasExited-polling nodig buiten wat hierin al gebeurt: elke klik checkt opnieuw.</summary>
    private readonly Dictionary<string, Process> _runningApps = [];

    private readonly List<AppEntry> _apps = LoadApps();

    /// <summary>De twee schermen waar het menu tussen schakelt. Eén keer gemaakt en hergebruikt, zodat wat je erin invult niet weg is als je even naar het andere scherm kijkt.</summary>
    private readonly InhoudSchermen.Hoofdscherm _hoofdscherm = new();

    private readonly SettingsView _settingsView = new();

    /// <summary>Het tokenoverzicht. Ook één keer gemaakt en hergebruikt; het ververst zichzelf als het weer in beeld komt.</summary>
    private readonly InhoudSchermen.Tokens _tokens = new();

    /// <summary>Het gezondheidsoverzicht. Kijkt mee met dezelfde monitor als de pil in de footer.</summary>
    private readonly InhoudSchermen.Health _health = new();

    /// <summary>De bewaking van deze app. Eén per applicatie: de pil en het gezondheidsscherm kijken allebei hiernaar.</summary>
    private HealthMonitor? _monitor;

    /// <summary>Het menu zelf, om te kunnen zeggen welke pagina open staat.</summary>
    private Navigatiemenu? _menu;

    /// <summary>De verbinding met de broker: levert de AI-instelling en de lijst met aanwezige modellen aan de gezondheidscontrole.</summary>
    private readonly IAiBrokerClient _broker = AiBrokerClient.CreateDefault();

    private BrokerLogReader? _brokerLog;

    public MainWindow()
    {
        InitializeComponent();

        // Hoort bij de WindowChrome in MainWindow.xaml: zonder titelbalk
        // maximaliseert Windows dit venster over de taakbalk heen.
        Layout.MaximizeBounds.Apply(this);

        // Lettertype en venstermaat kunnen pas als er een venster is; de
        // rest is in App.OnStartup al gezet.
        Layout.Preferences.ApplyToWindow(this);

        // Waar het venster stond onthouden we bij het sluiten - net als de
        // splitterposities, zodat je het niet elke keer opnieuw neerzet.
        Closing += (_, _) => Layout.Preferences.BewaarVenster(this);

        BuildMenu();
        WireActionRail();
        ShowContent(NavigatiemenuItem.Main);
        StartActivityLog();
        StartHealthCheck();

        // Een ander AI-model kan de gezondheid omgooien - opnieuw
        // controleren dus, zodat de pil klopt met wat er net gekozen is.
        _settingsView.Instellingen.Ai.SettingsSaved += (_, _) => StartHealthCheck();
    }

    /// <summary>
    /// Leest de Apps-lijst uit appsettings.json naast de exe - gewoon
    /// System.Text.Json op een platte array, geen
    /// Microsoft.Extensions.Configuration nodig voor zoiets simpels
    /// (geen geneste/typed settings-binding, alleen een lijst objecten).
    ///
    /// Sinds het menu uit vaste knoppen bestaat wordt hier alleen nog het
    /// PAD naar Stylebook uit gehaald; de lijst wordt niet meer als menu
    /// getoond. De overige entries blijven staan voor als ze een knop
    /// krijgen.
    /// </summary>
    private static List<AppEntry> LoadApps()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        var apps = document.RootElement.GetProperty("Apps").Deserialize<List<AppEntry>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return apps ?? [];
    }

    /// <summary>
    /// Zet het gedeelde Navigatiemenu in het Menu-vak. Het menu meldt
    /// alleen WELKE knop is aangeklikt; wat daarop gebeurt staat hier,
    /// zodat het component zelf niets van Jabasoft hoeft te weten.
    ///
    /// In code-behind en niet rechtstreeks in XAML, om dezelfde reden als
    /// voorheen: een x:Name'd element dat als property-waarde aan
    /// Hoofdscherm wordt meegegeven botst met diens naam-scope (MC3093,
    /// zie MainWindow.xaml).
    /// </summary>
    private void BuildMenu()
    {
        _menu = new Navigatiemenu();

        // Uit de gebouwde assembly en niet overgetypt in de XAML: dan kan het
        // getal op het scherm niet afwijken van de code die draait. Ophogen
        // gebeurt in <Version> in Jabasoft.App.csproj.
        _menu.Version = $"V {AppVersion.Current}";

        AutomationProperties.SetName(_menu, "Hoofdmenu");
        _menu.ItemSelected += (_, item) => Navigate(item);
        AppShell.MenuContent = _menu;
    }

    /// <summary>
    /// Vult het Activity-blok: de eigen regels van deze app komen
    /// rechtstreeks in de gedeelde opvang, en BrokerLogReader haalt die van
    /// de AI-broker erbij. Die laatste loopt door zolang de app leeft, ook
    /// als de broker tussendoor even weg is.
    /// </summary>
    private void StartActivityLog()
    {
        ActivityLog.Shared.Add("app", "Jabasoft gestart");

        _brokerLog = new BrokerLogReader(ActivityLog.Shared);
        _brokerLog.Start();
    }

    /// <summary>
    /// Zet de SYSTEM HEALTH-pil aan het werk. WELKE controles er lopen is
    /// een keuze van deze app - het bewaken zelf zit in Jabasoft.Base, zodat
    /// elke applicatie het op dezelfde manier doet.
    ///
    /// De pil staat op oranje "Checking AI broker" zolang de controle loopt.
    /// Draait de broker niet, dan START de controle hem - het is een
    /// gedeelde voorziening waar meerdere applicaties op leunen. De pil
    /// zegt dan "Starting AI broker" en blijft oranje: bezig is niet fout.
    /// Pas als hij niet omhoog komt wordt het rood.
    /// </summary>
    private void StartHealthCheck()
    {
        if (AppShell.Footer is not { } footer)
        {
            return;
        }

        // De monitor wordt één keer gemaakt en daarna hergebruikt: het
        // gezondheidsscherm hangt eraan, en "opnieuw controleren" laat
        // dezelfde monitor nog eens lopen.
        if (_monitor is null)
        {
            // Op volgorde van afhankelijkheid: eerst de broker (die wordt zo
            // nodig opgestart, en blijft zolang oranje), dan de AI-server
            // erachter, dan of de ingestelde modellen daar echt staan, en
            // tot slot de gedeelde database. Ze worden allemaal gedaan, ook
            // als er onderweg een faalt - zie HealthMonitor.
            _monitor = new HealthMonitor(
            [
                new AiBrokerHealthCheck(),
                new AiServerHealthCheck(_broker),
                new AiModelsHealthCheck(_broker),
                new DatabaseHealthCheck(_broker),
            ]);

            footer.Observe(_monitor);
            _health.Observe(_monitor);

            // Elke stap van de controle ook in het Activity-blok, zodat je daar
            // terugleest wat er bij het opstarten gebeurd is.
            _monitor.Changed += (_, status) => ActivityLog.Shared.Add("app", status.Message);
        }

        // Niet awaiten: het venster mag meteen verschijnen, de pil vult
        // zichzelf bij terwijl de controle loopt.
        _ = _monitor.RunAsync();
    }

    private void Navigate(NavigatiemenuItem item)
    {
        // Stylebook en AI Studio zijn geen schermen van deze app maar eigen
        // applicaties; de rest schakelt het inhoudsvak om.
        switch (item)
        {
            case NavigatiemenuItem.Stylebook:
                StartApp(StylebookAppName);
                return;
            case NavigatiemenuItem.AiStudio:
                GaNaarApp(AiStudioAppName);
                return;
            default:
                ShowContent(item);
                return;
        }
    }

    /// <summary>
    /// Zet het inhoudsvak op één van de schermen. Alle wegen ernaartoe
    /// (menu én de knop in de Actie-rail) lopen hierlangs, zodat die twee
    /// elkaar niet tegenspreken.
    /// </summary>
    private void ShowContent(NavigatiemenuItem item)
    {
        AppShell.MainContent = item switch
        {
            NavigatiemenuItem.Settings => _settingsView,
            NavigatiemenuItem.Tokens => _tokens,
            NavigatiemenuItem.Health => _health,
            _ => _hoofdscherm,
        };

        // Het menu markeert de knop van de pagina die nu open staat. Hier en
        // niet bij de klik: ook een schakeling die ergens anders vandaan komt
        // (de actierail, of straks een knop op het hoofdscherm) komt hierlangs.
        if (_menu is not null)
        {
            _menu.Active = item;
        }
    }

    /// <summary>
    /// De actierail zit ingebakken in de paginaschil, dus hij hoeft hier
    /// niet opgebouwd te worden - alleen aangesloten. De rail meldt wat er
    /// aangeklikt is; wat dat betekent staat hier.
    ///
    /// Verversen zet de splitters in de header en de footer terug op hun
    /// standaardstand. Meer zit er niet in de rail: de instellingen gaan
    /// via het menu.
    /// </summary>
    private void WireActionRail()
    {
        if (AppShell.Actie is not { } rail)
        {
            return;
        }

        rail.RefreshRequested += (_, _) => AppShell.ResetSplitters();
    }

    /// <summary>
    /// Geeft het scherm over aan een andere applicatie van de familie: die
    /// komt op DEZELFDE plek en grootte te staan, en dit venster verdwijnt
    /// van het scherm. Zo voelt het als één venster dat van inhoud wisselt.
    ///
    /// Lukt het overgeven niet, dan blijft alles staan zoals het stond - je
    /// blijft dus nooit met een leeg scherm achter.
    /// </summary>
    private void GaNaarApp(string naam)
    {
        if (_apps.FirstOrDefault(app => app.Name == naam) is not { Available: true } doel)
        {
            ActivityLog.Shared.Add("app", $"{naam} staat niet (of niet als beschikbaar) in appsettings.json.");
            return;
        }

        // De venstermaat nog even bewaren: de andere applicatie neemt hem
        // over, en zo staat hij ook in onze eigen voorkeuren als we straks
        // weer tevoorschijn komen.
        Layout.Preferences.BewaarVenster(this);

        var handvat = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (!AppHandover.SwitchTo(doel.ExecutablePath, handvat))
        {
            ActivityLog.Shared.Add("app", $"{naam} kon niet geopend worden - het venster is niet gevonden.");
        }
    }

    private void StartApp(string naam)
    {
        if (_apps.FirstOrDefault(app => app.Name == naam) is { Available: true } app2)
        {
            StartOrFocusApp(app2);
            return;
        }

        ActivityLog.Shared.Add("app", $"{naam} staat niet (of niet als beschikbaar) in appsettings.json.");
    }

    /// <summary>
    /// Start de app als 'ie nog niet draait, of haalt anders het
    /// bestaande venster naar voren - geen dubbele exemplaren. Jabasoft
    /// is een launcher, geen container: dit start een apart proces/
    /// venster, embedt niets. Gestarte processen worden bewust NIET
    /// gekilld als Jabasoft zelf afsluit (zie MainWindow.xaml.cs-comment
    /// in het plan) - het zijn zelfstandige apps, geen subprocessen van
    /// een hostende shell.
    /// </summary>
    private void StartOrFocusApp(AppEntry app)
    {
        if (_runningApps.TryGetValue(app.Name, out var existing) && !existing.HasExited)
        {
            if (existing.MainWindowHandle != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(existing.MainWindowHandle);
            }

            return;
        }

        // Draait hij al buiten ons om - je hebt hem zelf van het bureaublad
        // gestart? Dan dat venster naar voren halen in plaats van een tweede
        // exemplaar erbij.
        var procesnaam = Path.GetFileNameWithoutExtension(app.ExecutablePath);
        var draaiend = Process.GetProcessesByName(procesnaam).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

        if (draaiend is not null)
        {
            NativeMethods.SetForegroundWindow(draaiend.MainWindowHandle);
            _runningApps[app.Name] = draaiend;
            return;
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = app.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(app.ExecutablePath),
            UseShellExecute = true,
        });

        if (process is not null)
        {
            _runningApps[app.Name] = process;
        }
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
