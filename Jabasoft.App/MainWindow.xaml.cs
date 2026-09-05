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

        var themeFolder = builder.Configuration["SharedUi:ThemeFolder"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Jabasoft.Shared", "Shared.UI", "wwwroot");
        var themeCssPath = Path.Combine(Path.GetFullPath(themeFolder), "jabasoft-theme.css");
        var shellFolderForApi = Path.Combine(AppContext.BaseDirectory, "Assets", "Shell");

        // Shared telemetry database: same connection string every JabaSoft
        // app points at, so this API reads whatever TabStudio/LocalAiStudio
        // (and Jabasoft itself, later) have recorded.
        var telemetryConnectionString =
            builder.Configuration.GetConnectionString("JabaSoftTelemetry")
            ?? "Server=localhost;Database=JabaSoftTelemetry;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

        builder.Services.AddDbContext<TelemetryDbContext>(options => options.UseSqlServer(telemetryConnectionString));
        builder.Services.AddScoped<ITokenUsageRepository, TokenUsageRepository>();
        builder.Services.AddHttpClient();

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

        // Backs the Stijlgids page: reads live from IConfiguration (which
        // reloads appsettings.json on change) so editing Apps:*:Pages there
        // and clicking "Pagina's verversen" picks up new pages without
        // restarting Jabasoft.
        _api.MapGet("/api/apps-config", (IConfiguration configuration) => Results.Ok(BuildAppsConfig(configuration)));

        // Also backs the Stijlgids page: the CSS editing strip reads/writes
        // the one canonical jabasoft-theme.css directly (Jabasoft.Shared/
        // Shared.UI/wwwroot), the same physical file every app loads - so a
        // save here is immediately live everywhere, no copy involved.
        _api.MapGet("/api/theme-css", async () =>
        {
            if (!File.Exists(themeCssPath))
            {
                return Results.NotFound();
            }

            return Results.Text(await File.ReadAllTextAsync(themeCssPath), "text/css");
        });

        _api.MapPut("/api/theme-css", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var content = await reader.ReadToEndAsync();
            await File.WriteAllTextAsync(themeCssPath, content);
            return Results.Ok();
        });

        // Stijlgids "kopiëren": a live cross-origin embed can't be
        // re-themed from here (TabStudio/LocalAiStudio's own document is
        // out of reach, and touching their source is out of scope - the
        // second theme is Jabasoft-local only, see vs-theme.css). So
        // instead of embedding the live app, this fetches each configured
        // page's current HTML *once* and saves it as a genuine local copy
        // under Assets/Shell/dummy-pages/ - same origin as the shell
        // itself, styled with the normal <link>/theme.js setup every other
        // Jabasoft page uses, no proxying at request time. These are
        // frozen snapshots, not live views: rerun this (the Stijlgids
        // "Pagina's verversen" button) after a real page's markup changes.
        _api.MapPost("/api/capture-pages", async (IConfiguration configuration, IHttpClientFactory httpClientFactory) =>
        {
            var dummyPagesFolder = Path.Combine(shellFolderForApi, "dummy-pages");
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var captured = new List<object>();
            foreach (var appSection in configuration.GetSection("Apps").GetChildren())
            {
                var baseUrl = appSection["MainUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl = appSection["DevelopmentUrl"];
                }

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    continue;
                }

                var appFolder = Path.Combine(dummyPagesFolder, appSection.Key);
                Directory.CreateDirectory(appFolder);

                foreach (var pageSection in appSection.GetSection("Pages").GetChildren())
                {
                    var path = pageSection["Path"];
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    var fileName = ToSafeFileName(path) + ".html";
                    try
                    {
                        var html = await client.GetStringAsync(baseUrl.TrimEnd('/') + path);
                        var injection =
                            $"<base href=\"{baseUrl.TrimEnd('/')}/\" />" +
                            "<link rel=\"stylesheet\" href=\"vs-theme.css\" />" +
                            "<script src=\"theme.js\"></script>";

                        var headIndex = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                        html = headIndex >= 0
                            ? html.Insert(headIndex + "<head>".Length, injection)
                            : injection + html;

                        await WriteFileWithRetryAsync(Path.Combine(appFolder, fileName), html);
                        captured.Add(new { app = appSection.Key, path, file = $"dummy-pages/{appSection.Key}/{fileName}", ok = true });
                    }
                    catch (Exception ex)
                    {
                        captured.Add(new { app = appSection.Key, path, error = ex.Message, ok = false });
                    }
                }
            }

            return Results.Ok(captured);
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
        var shellFolder = shellFolderForApi;

        await WebView.EnsureCoreWebView2Async();
        SetupVirtualHosts(themeFolder, shellFolder);

        // Show a "starting up" page immediately - EnsureAppsRunningAsync
        // below can take a while the first time (cold "dotnet run" build),
        // and the window would otherwise sit blank until it's done.
        WebView.CoreWebView2.Navigate("https://app.jabasoft.local/loading.html");

        await EnsureAppsRunningAsync(configuration);

        WriteShellConfig(configuration, apiBaseUrl, shellFolder);
        WebView.CoreWebView2.Navigate("https://app.jabasoft.local/shell.html");
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
        var json = JsonSerializer.Serialize(new { apps = BuildAppsConfig(configuration), apiBaseUrl });
        File.WriteAllText(Path.Combine(shellFolder, "config.js"), $"window.jabasoftConfig = {json};");
    }

    /// <summary>
    /// Reads the Apps section fresh from IConfiguration every call (the
    /// default appsettings.json provider reloads on file change), so both
    /// the one-time config.js write above and the live /api/apps-config
    /// endpoint (used by the Stijlgids page's "Pagina's verversen" button)
    /// share the same shape and both reflect edits without a restart.
    /// </summary>
    private static Dictionary<string, object> BuildAppsConfig(IConfiguration configuration)
    {
        var apps = new Dictionary<string, object>();
        foreach (var appSection in configuration.GetSection("Apps").GetChildren())
        {
            var pages = new List<object>();
            foreach (var pageSection in appSection.GetSection("Pages").GetChildren())
            {
                var path = pageSection["Path"];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                pages.Add(new
                {
                    path,
                    label = pageSection["Label"] ?? path,
                    file = $"dummy-pages/{appSection.Key}/{ToSafeFileName(path)}.html",
                });
            }

            apps[appSection.Key] = new
            {
                displayName = appSection["DisplayName"] ?? appSection.Key,
                developmentUrl = appSection["DevelopmentUrl"],
                mainUrl = appSection["MainUrl"],
                pages,
            };
        }

        return apps;
    }

    /// <summary>
    /// A freshly-written file in this folder is occasionally still briefly
    /// locked (observed with Windows file-system scanners) right after
    /// creation, so a plain WriteAllTextAsync can spuriously fail here.
    /// Retried a few times with a short backoff before giving up for real.
    /// </summary>
    private static async Task WriteFileWithRetryAsync(string path, string content)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await File.WriteAllTextAsync(path, content);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(200 * attempt);
            }
        }
    }

    /// <summary>Turns a route like "/" or "/songs/{Id}" into a plain file-name-safe token ("root", "songs-id").</summary>
    private static string ToSafeFileName(string path)
    {
        if (path == "/")
        {
            return "root";
        }

        var chars = path.Trim('/').ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = '-';
            }
        }

        return new string(chars).ToLowerInvariant();
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
