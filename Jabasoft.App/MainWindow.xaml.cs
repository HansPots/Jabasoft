using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.FileProviders;
using Microsoft.Web.WebView2.Core;
using Shared.Telemetry;

namespace Jabasoft.App;

/// <summary>
/// The whole shell is a single WebView2 control. Native XAML is just the
/// window frame - the header/menu/content chrome is HTML/CSS loaded from
/// Assets/Shell, styled by the one shared jabasoft-theme.css from
/// Jabasoft.Stylebook/Shared.UI (mapped in directly, not copied). Embedded
/// apps (TabStudio, LocalAiStudio, Stylebook) show inside an &lt;iframe&gt;
/// in that page - they're already ordinary web apps, so no extra native
/// WebView2 instances are needed per app.
/// </summary>
public partial class MainWindow : Window
{
    private WebApplication? _api;
    private readonly List<Process> _startedAppProcesses = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var builder = WebApplication.CreateBuilder();

        var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5300";
        builder.WebHost.UseUrls(apiBaseUrl);

        var themeFolder = builder.Configuration["SharedUi:ThemeFolder"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Jabasoft.Stylebook", "Shared.UI", "wwwroot");
        var shellFolderForApi = Path.Combine(AppContext.BaseDirectory, "Assets", "Shell");

        // Shared telemetry database: same connection string every JabaSoft
        // app points at, so this API reads whatever TabStudio/LocalAiStudio
        // (and Jabasoft itself, later) have recorded.
        var telemetryConnectionString =
            builder.Configuration.GetConnectionString("JabasoftBase")
            ?? "Server=localhost;Database=JabasoftBase;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

        builder.Services.AddDbContext<TelemetryDbContext>(options => options.UseSqlServer(telemetryConnectionString));
        builder.Services.AddScoped<ITokenUsageRepository, TokenUsageRepository>();

        // Token verbruik: the BlazorWebView control shares this same DI
        // container (see TokenDashboardView.Services below), so
        // TokenUsageOverview (Jabasoft.Base) reads from the exact same
        // ITokenUsageRepository TabStudio/LocalAiStudio write to - same
        // data, Jabasoft-only presentation.
        builder.Services.AddWpfBlazorWebView();
        builder.Services.AddSingleton<JabasoftHostBridge>();

        // The dashboard page is fetched from a WebView2 virtual host
        // (https://app.jabasoft.local), a different origin than this API
        // (http://localhost:5300), so a permissive local CORS policy is
        // needed purely for that same-machine call.
        builder.Services.AddCors(options => options.AddDefaultPolicy(
            policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        _api = builder.Build();
        _api.UseCors();

        // Exposes Assets/Shell over plain HTTP too (same folder as the
        // WebView2 virtual host mapping below), purely so Stylebook's
        // "Pagina's verversen" (see Jabasoft.Stylebook/Stylebook.Web/
        // Program.cs's /api/capture-pages) can fetch a snapshot of it -
        // that capture runs server-side (HttpClient), which can't reach
        // a WebView2-only virtual host at all. Jabasoft's own shell is
        // never viewed via this URL, only captured through it.
        _api.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(shellFolderForApi),
            RequestPath = "",
        });

        try
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TelemetryDbContext>().Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not apply migrations for the shared JabasoftBase database: {ex.Message}");
        }

        _ = _api.RunAsync();

        var configuration = builder.Configuration;
        var shellFolder = shellFolderForApi;

        // WebView (the shell) and TokenDashboardView (BlazorWebView) are two
        // separate WebView2 instances in one process. Left to their own
        // defaults they'd both resolve the same default user-data folder
        // (derived from the exe path), and two CoreWebView2 environments
        // can't cleanly share one folder - that's exactly the kind of
        // collision that leaves one control rendering fine (the .NET-to-JS
        // direction still works, so content still appears) while its
        // JS-to-.NET event channel - the thing @onclick depends on -
        // silently never delivers anything back. Giving each control its
        // own explicit, distinct folder removes the collision entirely.
        var shellEnvironment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine(AppContext.BaseDirectory, "WebView2Data", "Shell"));

        TokenDashboardView.BlazorWebViewInitializing += (_, args) =>
            args.UserDataFolder = Path.Combine(AppContext.BaseDirectory, "WebView2Data", "Dashboard");

        await WebView.EnsureCoreWebView2Async(shellEnvironment);
        SetupVirtualHosts(themeFolder, shellFolder);

        // Show a "starting up" page immediately - EnsureAppsRunningAsync
        // below can take a while the first time (cold "dotnet run" build),
        // and the window would otherwise sit blank until it's done.
        WebView.CoreWebView2.Navigate("https://app.jabasoft.local/loading.html");

        // Token verbruik: shell.js posts "show-token-dashboard" instead of
        // navigating its content iframe there (see shell.js), since that
        // page is now BlazorWebView-hosted, a native sibling control, not
        // HTML inside this WebView2. JabasoftHostBridge carries the
        // opposite direction (BlazorWebView -> "go back to the shell").
        WebView.CoreWebView2.WebMessageReceived += (_, args) =>
        {
            switch (args.TryGetWebMessageAsString())
            {
                case "show-token-dashboard":
                    ShowTokenDashboard();
                    break;
                case "hide-token-dashboard":
                    TokenDashboardView.Visibility = Visibility.Collapsed;
                    WebView.Visibility = Visibility.Visible;
                    break;
            }
        };

        TokenDashboardView.Services = _api.Services;
        TokenDashboardView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(TokenDashboardRoot),
        });

        _api.Services.GetRequiredService<JabasoftHostBridge>().BackToShellRequested += ShowShell;

        await EnsureAppsRunningAsync(configuration);

        WriteShellConfig(configuration, apiBaseUrl, shellFolder);
        WebView.CoreWebView2.Navigate("https://app.jabasoft.local/shell.html");
    }

    private void ShowTokenDashboard()
    {
        // WebMessageReceived already fires on the WPF UI thread, but this
        // keeps the method safe to call from anywhere.
        Dispatcher.Invoke(() =>
        {
            // WebView2 and BlazorWebView are both native (HWND/DirectComposition
            // -hosted) controls stacked in the same Grid cell. Visibility alone
            // controls WPF hit-testing for ordinary elements, but these "airspace"
            // controls can keep intercepting pointer input even while Collapsed -
            // IsHitTestVisible=false plus an explicit focus hand-off makes sure
            // input actually reaches the one that's supposed to be on top.
            // Hidden (not Collapsed) so both controls always occupy real layout
            // space - a composition-hosted control that starts at a 0x0
            // Collapsed size may never establish a working input/hit-test
            // surface, and Visibility alone won't fix that after the fact.
            WebView.IsHitTestVisible = false;
            WebView.Visibility = Visibility.Hidden;
            TokenDashboardView.Visibility = Visibility.Visible;
            TokenDashboardView.IsHitTestVisible = true;
            TokenDashboardView.Focus();
        });
    }

    private void ShowShell()
    {
        // JabasoftHostBridge.BackToShellRequested fires from the Blazor
        // Hybrid component's own dispatcher, not necessarily the WPF UI
        // thread, so this one actually needs the marshal.
        Dispatcher.Invoke(() =>
        {
            TokenDashboardView.IsHitTestVisible = false;
            TokenDashboardView.Visibility = Visibility.Hidden;
            WebView.Visibility = Visibility.Visible;
            WebView.IsHitTestVisible = true;
            WebView.Focus();
        });
    }

    private void SetupVirtualHosts(string themeFolder, string shellFolder)
    {
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "shared.jabasoft.local", Path.GetFullPath(themeFolder), CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.jabasoft.local", shellFolder, CoreWebView2HostResourceAccessKind.Allow);
    }

    /// <summary>
    /// Starts "dotnet run" for any configured app (see appsettings.json's
    /// Apps:*:ProjectPath) whose DevelopmentUrl isn't already answering -
    /// so opening Jabasoft is enough on its own, without having to start
    /// TabStudio/LocalAiStudio/Stylebook by hand first. An app already
    /// running (started manually, or from a previous Jabasoft session) is
    /// left alone. Processes started here are tracked so OnClosed can stop
    /// them when the shell closes.
    /// </summary>
    private async Task EnsureAppsRunningAsync(IConfiguration configuration)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var appsToWaitFor = new List<string>();

        foreach (var appSection in configuration.GetSection("Apps").GetChildren())
        {
            var url = appSection["DevelopmentUrl"];
            var projectPath = appSection["ProjectPath"];
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            appsToWaitFor.Add(url);

            if (await IsReachableAsync(httpClient, url))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            {
                Debug.WriteLine($"Skipping auto-start for '{appSection.Key}': ProjectPath is not configured or doesn't exist.");
                continue;
            }

            StartApp(appSection.Key, projectPath, url);
        }

        // Give freshly started apps a chance to come up (first "dotnet run"
        // includes a build) before navigating the shell to it.
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            var allUp = true;
            foreach (var url in appsToWaitFor)
            {
                if (!await IsReachableAsync(httpClient, url))
                {
                    allUp = false;
                    break;
                }
            }

            if (allUp)
            {
                break;
            }

            await Task.Delay(500);
        }
    }

    private static async Task<bool> IsReachableAsync(HttpClient client, string url)
    {
        try
        {
            using var response = await client.GetAsync(url);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartApp(string name, string projectPath, string url)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(url);
        startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";

        try
        {
            var process = Process.Start(startInfo);
            if (process is not null)
            {
                _startedAppProcesses.Add(process);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not start '{name}' from '{projectPath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Writes config.js into the *output* Assets/Shell folder (never the
    /// source folder under source control) so shell.js/dashboard.js can
    /// read the configured app URLs and API base URL without a build step.
    /// </summary>
    private static void WriteShellConfig(IConfiguration configuration, string apiBaseUrl, string shellFolder)
    {
        var json = JsonSerializer.Serialize(new { apps = BuildAppsConfig(configuration), apiBaseUrl });
        File.WriteAllText(Path.Combine(shellFolder, "config.js"), $"window.jabasoftConfig = {json};");
    }

    /// <summary>
    /// Reads the Apps section fresh from IConfiguration every call (the
    /// default appsettings.json provider reloads on file change) so the
    /// shell's menu (shell.js) reflects edits without a restart.
    /// </summary>
    private static Dictionary<string, object> BuildAppsConfig(IConfiguration configuration)
    {
        var apps = new Dictionary<string, object>();
        foreach (var appSection in configuration.GetSection("Apps").GetChildren())
        {
            apps[appSection.Key] = new
            {
                displayName = appSection["DisplayName"] ?? appSection.Key,
                developmentUrl = appSection["DevelopmentUrl"],
                mainUrl = appSection["MainUrl"],
            };
        }

        return apps;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _ = _api?.StopAsync();

        foreach (var process in _startedAppProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    // "dotnet run" launches the actual app as a child
                    // process, so the whole tree needs killing, not just
                    // the "dotnet run" wrapper.
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort cleanup - nothing useful to do if this fails.
            }
        }
    }
}
