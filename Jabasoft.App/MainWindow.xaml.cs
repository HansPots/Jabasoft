using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Jabasoft.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Gestarte processen per AppEntry.Name - zie StartOrFocusApp. Geen HasExited-polling nodig buiten wat hierin al gebeurt: elke klik checkt opnieuw.</summary>
    private readonly Dictionary<string, Process> _runningApps = [];

    public MainWindow()
    {
        InitializeComponent();
        BuildAppList(LoadApps());
    }

    /// <summary>
    /// Leest de Apps-lijst uit appsettings.json naast de exe - gewoon
    /// System.Text.Json op een platte array, geen
    /// Microsoft.Extensions.Configuration nodig voor zoiets simpels
    /// (geen geneste/typed settings-binding, alleen een lijst objecten).
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
    /// Bouwt de rijen in code-behind en wijst het geheel toe aan
    /// AppShell.MenuContent - kan niet als x:Name'd element rechtstreeks
    /// in XAML onder Basis.MenuContent staan (MC3093, zie MainWindow.xaml).
    /// </summary>
    private void BuildAppList(List<AppEntry> apps)
    {
        var list = new StackPanel { Margin = new Thickness(16) };

        foreach (var app in apps)
        {
            var button = new Button
            {
                Style = (Style)FindResource("AppEntryButtonStyle"),
                Content = app.Available ? app.DisplayName : $"{app.DisplayName} (nog niet herbouwd)",
                IsEnabled = app.Available,
            };
            AutomationProperties.SetName(button, app.DisplayName);
            button.Click += (_, _) => StartOrFocusApp(app);
            list.Children.Add(button);
        }

        AppShell.MenuContent = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list };
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
