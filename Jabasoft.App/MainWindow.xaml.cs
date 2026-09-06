using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Web.WebView2.Core;
using Jabasoft.Base.AiBroker;
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
        Width = 1400;
        Height = 900;
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

        // Components are persistent design data (like jabasoft-theme.css),
        // not disposable snapshots like dummy-pages/config.js below - they
        // need to live in source control, so they're read/written directly
        // in the *source* Assets/Shell folder, never the build output copy.
        var sourceShellFolder = builder.Configuration["Stijlgids:SourceShellFolder"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Shell");

        // Dummy-page copies are disposable snapshots, regenerated on demand
        // by "Pagina's verversen" - but a copy captured before a code
        // change (e.g. vs-theme.css/theme.js moving to Jabasoft.Shared) can
        // silently keep pointing at a path that no longer resolves, and
        // then just looks like theming "doesn't work" with no visible
        // error. Clearing them on every startup means what you see always
        // reflects the current code, at the cost of one re-capture click.
        var dummyPagesFolderAtStartup = Path.Combine(shellFolderForApi, "dummy-pages");
        if (Directory.Exists(dummyPagesFolderAtStartup))
        {
            Directory.Delete(dummyPagesFolderAtStartup, recursive: true);
        }

        // Shared telemetry database: same connection string every JabaSoft
        // app points at, so this API reads whatever TabStudio/LocalAiStudio
        // (and Jabasoft itself, later) have recorded.
        var telemetryConnectionString =
            builder.Configuration.GetConnectionString("JabasoftBase")
            ?? "Server=localhost;Database=JabasoftBase;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

        builder.Services.AddDbContext<TelemetryDbContext>(options => options.UseSqlServer(telemetryConnectionString));
        builder.Services.AddScoped<ITokenUsageRepository, TokenUsageRepository>();
        builder.Services.AddHttpClient();

        // Stijlgids "Genereer CSS met AI": same shared broker TabStudio/
        // LocalAiStudio use, not a separate AI integration. Jabasoft has no
        // AiConnectorSettings database of its own (see ai-connector.json
        // below), so this is only wired up here, not exposed as a general
        // chat feature.
        builder.Services.AddHttpClient<IAiBrokerClient, AiBrokerClient>(c => c.BaseAddress = new Uri(AiBrokerClient.DefaultBaseUrl));

        // Token verbruik: the BlazorWebView control shares this same DI
        // container (see TokenDashboardView.Services below), so
        // TokenUsageOverview (Jabasoft.App/TokenUsageOverview.razor) reads
        // from the exact same ITokenUsageRepository TabStudio/LocalAiStudio
        // write to via Jabasoft.Base.TokenUsageDashboard - same data,
        // Jabasoft-only presentation.
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

        // Starts Jabasoft.Broker if no instance is reachable yet (any
        // JabaSoft app can be the one that starts it) - fire-and-forget so
        // a cold broker build doesn't delay Jabasoft's own startup. See
        // Jabasoft.Base/AiBroker/AiBrokerProcessLauncher.cs.
        _ = AiBrokerProcessLauncher.EnsureRunningAsync();

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

        // ---------- Stijlgids: components (select-in-preview -> named,
        // separately-stylable component -> optionally materialized as a
        // real Blazor component in Jabasoft.Base) ----------
        var componentsFolder = Path.Combine(Path.GetFullPath(sourceShellFolder), "components");
        Directory.CreateDirectory(componentsFolder);
        var componentsIndexPath = Path.Combine(componentsFolder, "index.json");
        var aiConnectorPath = Path.Combine(AppContext.BaseDirectory, "ai-connector.json");
        var jabasoftBaseProjectPath = builder.Configuration["JabasoftBaseProject:ProjectPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Jabasoft.Base");

        _api.MapGet("/api/components", async () => Results.Ok(await ReadComponentIndexAsync(componentsIndexPath)));

        _api.MapPost("/api/components", async (ComponentCreateRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Geef het component een naam.");
            }

            var safeName = ToSafeComponentName(request.Name);
            await WriteFileWithRetryAsync(Path.Combine(componentsFolder, $"{safeName}.html"), request.Html ?? string.Empty);

            var cssPath = Path.Combine(componentsFolder, $"{safeName}.css");
            if (!File.Exists(cssPath))
            {
                await WriteFileWithRetryAsync(cssPath, string.Empty);
            }

            var index = await ReadComponentIndexAsync(componentsIndexPath);
            index.RemoveAll(c => c.Name.Equals(safeName, StringComparison.OrdinalIgnoreCase));
            index.Add(new ComponentInfo(safeName, request.SourceApp ?? "", request.SourcePath ?? "", DateTimeOffset.UtcNow));
            await WriteFileWithRetryAsync(componentsIndexPath, JsonSerializer.Serialize(index));

            return Results.Ok(new { name = safeName });
        });

        _api.MapGet("/api/components/{name}/html", async (string name) =>
        {
            var path = Path.Combine(componentsFolder, $"{ToSafeComponentName(name)}.html");
            return File.Exists(path) ? Results.Text(await File.ReadAllTextAsync(path), "text/html") : Results.NotFound();
        });

        _api.MapGet("/api/components/{name}/css", async (string name) =>
        {
            var path = Path.Combine(componentsFolder, $"{ToSafeComponentName(name)}.css");
            return File.Exists(path) ? Results.Text(await File.ReadAllTextAsync(path), "text/css") : Results.NotFound();
        });

        _api.MapPut("/api/components/{name}/css", async (string name, HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var content = await reader.ReadToEndAsync();
            await WriteFileWithRetryAsync(Path.Combine(componentsFolder, $"{ToSafeComponentName(name)}.css"), content);
            return Results.Ok();
        });

        _api.MapPost("/api/components/{name}/generate-css", async (string name, ComponentGenerateCssRequest request, IAiBrokerClient broker) =>
        {
            var safeName = ToSafeComponentName(name);
            var htmlPath = Path.Combine(componentsFolder, $"{safeName}.html");
            if (!File.Exists(htmlPath))
            {
                return Results.NotFound();
            }

            var html = await File.ReadAllTextAsync(htmlPath);
            var settings = await ReadAiConnectorSettingsAsync(aiConnectorPath);
            var themeExcerpt = File.Exists(themeCssPath) ? await File.ReadAllTextAsync(themeCssPath) : "";
            // The tokens (colors/spacing) are declared once near the top of
            // the shared stylesheet - a short excerpt is enough context for
            // a model to pick matching colors without pasting the whole file.
            var rootBlockEnd = themeExcerpt.IndexOf('}');
            var themeTokens = rootBlockEnd > 0 ? themeExcerpt[..(rootBlockEnd + 1)] : themeExcerpt;

            var systemPrompt =
                "Je schrijft CSS voor één UI-component van de JabaSoft-huisstijl. " +
                "Gebruik de opgegeven kleur-/spacing-tokens (CSS custom properties) waar passend. " +
                "Antwoord ALLEEN met de CSS, geen uitleg, geen markdown-codeblok.";
            var userPrompt =
                $"HTML van het component:\n{html}\n\n" +
                $"Beschikbare tokens uit jabasoft-theme.css:\n{themeTokens}\n\n" +
                $"Instructies: {(string.IsNullOrWhiteSpace(request.Instructions) ? "maak een nette, opgeruimde stijl passend bij de huisstijl." : request.Instructions)}";

            var result = await broker.ChatAsync(
                new ChatRequest(
                    ParseProvider(settings.Provider),
                    settings.ServerUrl,
                    settings.Model,
                    [new Jabasoft.Base.AiBroker.ChatMessage("system", systemPrompt), new Jabasoft.Base.AiBroker.ChatMessage("user", userPrompt)],
                    "Jabasoft"),
                CancellationToken.None);

            if (!result.Success)
            {
                return Results.Ok(new { success = false, errorMessage = result.ErrorMessage });
            }

            return Results.Ok(new { success = true, css = StripMarkdownCodeFence(result.Reply) });
        });

        _api.MapPost("/api/components/{name}/materialize", async (string name) =>
        {
            var safeName = ToSafeComponentName(name);
            var htmlPath = Path.Combine(componentsFolder, $"{safeName}.html");
            var cssPath = Path.Combine(componentsFolder, $"{safeName}.css");
            if (!File.Exists(htmlPath))
            {
                return Results.NotFound();
            }

            var pascalName = ToPascalCase(safeName);
            var html = await File.ReadAllTextAsync(htmlPath);
            var css = File.Exists(cssPath) ? await File.ReadAllTextAsync(cssPath) : "";

            var razorPath = Path.Combine(Path.GetFullPath(jabasoftBaseProjectPath), $"{pascalName}.razor");
            var razorCssPath = Path.Combine(Path.GetFullPath(jabasoftBaseProjectPath), $"{pascalName}.razor.css");
            await WriteFileWithRetryAsync(razorPath, html);
            await WriteFileWithRetryAsync(razorCssPath, css);

            return Results.Ok(new { razorPath, razorCssPath, usageSnippet = $"<{pascalName} />" });
        });

        // ---------- Jabasoft's own AI Connector settings (LM Studio by
        // default) - same shape as TabStudio's/LocalAiStudio's
        // AiConnectorSettings, but Jabasoft has no SQL database of its own
        // to store it in, so it's a small runtime-writable JSON file next
        // to the exe instead (same idea as config.js). ----------
        _api.MapGet("/api/ai-connector", async () => Results.Ok(await ReadAiConnectorSettingsAsync(aiConnectorPath)));

        _api.MapPut("/api/ai-connector", async (AiConnectorSettings settings) =>
        {
            await WriteFileWithRetryAsync(aiConnectorPath, JsonSerializer.Serialize(settings));
            return Results.Ok();
        });

        _api.MapGet("/api/ai-models", async (string provider, string serverUrl, IAiBrokerClient broker) =>
            Results.Ok(await broker.ListModelsAsync(ParseProvider(provider), serverUrl, CancellationToken.None)));

        _api.MapPost("/api/ai-test-connection", async (AiConnectorSettings settings, IAiBrokerClient broker) =>
            Results.Ok(await broker.TestConnectionAsync(ParseProvider(settings.Provider), settings.ServerUrl, settings.Model, CancellationToken.None)));

        // Stijlgids "kopiëren": a live cross-origin embed can't be
        // re-themed from here (TabStudio/LocalAiStudio's own document is
        // out of reach, and touching their source is out of scope while
        // the second theme is still being tuned - see Jabasoft.Shared/
        // Shared.UI/wwwroot/vs-theme.css). So instead of embedding the
        // live app, this fetches each configured page's current HTML
        // *once* and saves it as a genuine local copy under Assets/Shell/
        // dummy-pages/ - same origin as the shell itself, styled with the
        // normal <link>/theme.js setup every other Jabasoft page uses
        // (from the shared.jabasoft.local virtual host, not a local copy),
        // no proxying at request time. These are frozen snapshots, not
        // live views: rerun this (the Stijlgids "Pagina's verversen"
        // button) after a real page's markup changes.
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
                            "<link rel=\"stylesheet\" href=\"https://shared.jabasoft.local/vs-theme.css\" />" +
                            "<script src=\"https://shared.jabasoft.local/theme.js\"></script>";

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

    private static async Task<List<ComponentInfo>> ReadComponentIndexAsync(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(indexPath);
        return JsonSerializer.Deserialize<List<ComponentInfo>>(json) ?? [];
    }

    private static async Task<AiConnectorSettings> ReadAiConnectorSettingsAsync(string path)
    {
        if (!File.Exists(path))
        {
            await WriteFileWithRetryAsync(path, JsonSerializer.Serialize(AiConnectorSettings.Default));
            return AiConnectorSettings.Default;
        }

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<AiConnectorSettings>(json) ?? AiConnectorSettings.Default;
    }

    private static AiProvider ParseProvider(string? provider) =>
        Enum.TryParse<AiProvider>(provider, ignoreCase: true, out var parsed) ? parsed : AiProvider.LmStudio;

    /// <summary>
    /// Strips a markdown code fence a model sometimes wraps its answer in
    /// (```css ... ```) despite being asked not to - kept lenient rather
    /// than failing the request, since the CSS itself still lands in the
    /// editor for the user to review either way.
    /// </summary>
    private static string StripMarkdownCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var withoutOpeningFence = trimmed[(firstNewline + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return (closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence).Trim();
    }

    /// <summary>Keeps letters/digits only (so it's safe as both a filename and a C# identifier); collapses everything else.</summary>
    private static string ToSafeComponentName(string name)
    {
        var chars = name.Where(char.IsLetterOrDigit).ToArray();
        var safe = new string(chars);
        return string.IsNullOrEmpty(safe) ? "Component" : safe;
    }

    /// <summary>Capitalizes the first letter for use as a Razor component/class/file name.</summary>
    private static string ToPascalCase(string safeName) =>
        char.ToUpperInvariant(safeName[0]) + safeName[1..];

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
