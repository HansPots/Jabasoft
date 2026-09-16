using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Stylebook.Components.Controls;
using InhoudSchermen = Stylebook.Components.Regions.Inhoud;

namespace Jabasoft.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>De naam in appsettings.json van de app die de Stylebook-knop start.</summary>
    private const string StylebookAppName = "Stylebook";

    /// <summary>Gestarte processen per AppEntry.Name - zie StartOrFocusApp. Geen HasExited-polling nodig buiten wat hierin al gebeurt: elke klik checkt opnieuw.</summary>
    private readonly Dictionary<string, Process> _runningApps = [];

    private readonly List<AppEntry> _apps = LoadApps();

    /// <summary>De twee schermen waar het menu tussen schakelt. Eén keer gemaakt en hergebruikt, zodat wat je erin invult niet weg is als je even naar het andere scherm kijkt.</summary>
    private readonly InhoudSchermen.Hoofdscherm _hoofdscherm = new();

    private readonly SettingsView _settingsView = new();

    private bool _showingSettings;

    public MainWindow()
    {
        InitializeComponent();
        BuildMenu();
        BuildActionContent();
        ShowContent(settings: false);
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
        var menu = new Navigatiemenu();
        AutomationProperties.SetName(menu, "Hoofdmenu");
        menu.ItemSelected += (_, item) => Navigate(item);
        AppShell.MenuContent = menu;
    }

    private void Navigate(NavigatiemenuItem item)
    {
        switch (item)
        {
            case NavigatiemenuItem.Stylebook:
                StartStylebook();
                break;
            case NavigatiemenuItem.Settings:
                ShowContent(settings: true);
                break;
            default:
                ShowContent(settings: false);
                break;
        }
    }

    /// <summary>Zet het inhoudsvak op één van de twee schermen. Alle wegen ernaartoe (menu én de knop in de Actie-rail) lopen hierlangs, zodat die twee elkaar niet tegenspreken.</summary>
    private void ShowContent(bool settings)
    {
        _showingSettings = settings;
        AppShell.MainContent = settings ? _settingsView : _hoofdscherm;
    }

    /// <summary>
    /// De instellingen-knop in de Actie-rail - zelfde reden als
    /// BuildMenu om dit in code-behind te doen i.p.v. rechtstreeks in
    /// XAML (MC3093, zie MainWindow.xaml). Klikken schakelt heen en weer
    /// tussen het hoofdscherm en de instellingen; hij deelt zijn stand met
    /// de Settings-knop in het menu.
    /// </summary>
    private void BuildActionContent()
    {
        var settingsButton = new Button
        {
            Style = (Style)FindResource("IconActionButtonStyle"),
            Content = new TextBlock
            {
                Text = (string)FindResource("IconSettings"),
                FontFamily = (FontFamily)FindResource("IconFontFamily"),
            },
        };
        AutomationProperties.SetName(settingsButton, "Instellingen");
        settingsButton.Click += (_, _) => ShowContent(!_showingSettings);

        AppShell.ActionContent = settingsButton;
    }

    private void StartStylebook()
    {
        if (_apps.FirstOrDefault(app => app.Name == StylebookAppName) is { Available: true } stylebook)
        {
            StartOrFocusApp(stylebook);
        }
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
