using System;
using System.Collections.Generic;
using System.IO;
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
            System.Diagnostics.Debug.WriteLine($"Could not apply migrations for the shared JabaSoftTelemetry database: {ex.Message}");
        }

        _ = _api.RunAsync();

        await InitializeWebViewAsync(builder.Configuration, apiBaseUrl);
    }

    private async Task InitializeWebViewAsync(IConfiguration configuration, string apiBaseUrl)
    {
        await WebView.EnsureCoreWebView2Async();

        var themeFolder = configuration["SharedUi:ThemeFolder"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Jabasoft.Shared", "Shared.UI", "wwwroot");
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "shared.jabasoft.local", Path.GetFullPath(themeFolder), CoreWebView2HostResourceAccessKind.Allow);

        var shellFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Shell");
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.jabasoft.local", shellFolder, CoreWebView2HostResourceAccessKind.Allow);

        WriteShellConfig(configuration, apiBaseUrl, shellFolder);

        WebView.CoreWebView2.Navigate("https://app.jabasoft.local/shell.html");
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
    }
}
