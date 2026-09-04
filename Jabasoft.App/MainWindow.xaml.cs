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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Shared.Telemetry;

namespace Jabasoft.App;

/// <summary>
/// The whole shell is a single WebView2 control. Native XAML is just the
/// window frame - the header/menu/content chrome is HTML/CSS loaded from
/// Assets/Shell, styled by the one shared jabasoft-theme.css from
/// Jabasoft.Shared/Shared.UI (mapped in directly, not copied). Embedded
/// apps (TabStudio, LocalAiStudio) show inside an &lt;iframe&gt; in that
/// page - they're already ordinary web apps, so no extra native WebView2
/// instances are needed per app.
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

        // Shared telemetry database: same connection string every JabaSoft
        // app points at, so this API reads whatever TabStudio/LocalAiStudio
        // (and Jabasoft itself, later) have recorded.
        var telemetryConnectionString =
            builder.Configuration.GetConnectionString("JabaSoftTelemetry")
            ?? "Server=localhost;Database=JabaSoftTelemetry;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

        builder.Services.AddDbContext<TelemetryDbContext>(options => options.UseSqlServer(telemetryConnectionString));
        builder.Services.AddScoped<ITokenUsageRepository, TokenUsageRepository>();

        // The dashboard page is fetched from a WebView2 virtual host
        // (https://app.jabasoft.local), a different origin than this API
        // (http://localhost:5300), so a permissive local CORS policy is
        // needed purely for that same-machine call.
        builder.Services.AddCors(options => options.AddDefaultPolicy(
            policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        _api = builder.Build();
        _api.UseCors();
        _api.MapGet("/api/token-usage", async (ITokenUsageRepository repository, CancellationToken cancellationToken) =>
        {
            var since = DateTimeOffset.UtcNow.AddDays(-30);
            var entries = await repository.GetAllEntriesAsync(since, cancellationToken);
            return Results.Ok(entries);
        });

        try
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TelemetryDbContext>().Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not apply migrations for the shared JabaSoftTelemetry database: {ex.Message}");
        }

        _ = _api.RunAsync();

        var configuration = builder.Configuration;
        var shellFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Shell");

        await WebView.EnsureCoreWebView2Async();
        SetupVirtualHosts(configuration, shellFolder);

        // Show a "starting up" page immediately - EnsureAppsRunningAsync
        // below can take a while the first time (cold "dotnet run" build),
        // and the window would otherwise sit blank until it's done.
        WebView.CoreWebView2.Navigate("https://app.jabasoft.local/loading.html");

        await EnsureAppsRunningAsync(configuration);

        WriteShellConfig(configuration, apiBaseUrl, shellFolder);
        WebView.CoreWebView2.Navigate("https://app.jabasoft.local/shell.html");
    }

    private void SetupVirtualHosts(IConfiguration configuration, string shellFolder)
    {
        var themeFolder = configuration["SharedUi:ThemeFolder"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Jabasoft.Shared", "Shared.UI", "wwwroot");
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "shared.jabasoft.local", Path.GetFullPath(themeFolder), CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.jabasoft.local", shellFolder, CoreWebView2HostResourceAccessKind.Allow);
    }

    /// <summary>
    /// Starts "dotnet run" for any configured app (see appsettings.json's
    /// Apps:*:ProjectPath) whose DevelopmentUrl isn't already answering -
    /// so opening Jabasoft is enough on its own, without having to start
    /// TabStudio/LocalAiStudio by hand first. An app already running
    /// (started manually, or from a previous Jabasoft session) is left
    /// alone. Processes started here are tracked so OnClosed can stop them
    /// when the shell closes.
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

        var json = JsonSerializer.Serialize(new { apps, apiBaseUrl });
        File.WriteAllText(Path.Combine(shellFolder, "config.js"), $"window.jabasoftConfig = {json};");
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
