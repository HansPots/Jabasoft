using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Jabasoft.App.Taal;
using Jabasoft.Base.AiBroker;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Ollamakaart.xaml - see that file for what it looks like.
///
/// Vraagt Ollama zelf (niet de broker) wat het nu doet: /api/version voor de
/// versie, /api/ps voor de modellen die in het geheugen staan. Het adres komt
/// uit de AI-instelling bij de broker. Ververst elke paar seconden zolang de
/// kaart in beeld is; een server die niet antwoordt is geen fout van dit
/// scherm, alleen een melding op de kaart.
/// </summary>
public partial class Ollamakaart : UserControl
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

    private readonly IAiBrokerClient _broker = AiBrokerClient.CreateDefault();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _bezig;

    public Ollamakaart()
    {
        InitializeComponent();

        _timer.Tick += async (_, _) => await VernieuwAsync();

        // Alleen verversen als de kaart zichtbaar is - een verborgen scherm
        // hoeft Ollama niet elke 5 seconden lastig te vallen.
        IsVisibleChanged += async (_, e) =>
        {
            if (e.NewValue is true)
            {
                _timer.Start();
                await VernieuwAsync();
            }
            else
            {
                _timer.Stop();
            }
        };
    }

    private Brush Kleur(string sleutel, Brush terugval) => TryFindResource(sleutel) as Brush ?? terugval;

    private async Task VernieuwAsync()
    {
        if (_bezig)
        {
            return;
        }

        _bezig = true;

        try
        {
            var instelling = await _broker.GetSettingsAsync(CancellationToken.None);
            var url = instelling.Settings.Ollama.Url.TrimEnd('/');

            string versie;
            List<Geladen> geladen = [];

            try
            {
                using var versieDoc = JsonDocument.Parse(await Http.GetStringAsync($"{url}/api/version"));
                versie = versieDoc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "?" : "?";

                using var psDoc = JsonDocument.Parse(await Http.GetStringAsync($"{url}/api/ps"));

                if (psDoc.RootElement.TryGetProperty("models", out var lijst))
                {
                    geladen = lijst.EnumerateArray().Select(Lees).ToList();
                }
            }
            catch (Exception fout) when (fout is HttpRequestException or TaskCanceledException or JsonException)
            {
                Bolletje.Fill = Kleur("HealthErrorBrush", Brushes.Red);
                StatusText.Text = Teksten.Vul(this, "T_OllamaNietBereikbaar", "Not reachable at {0}", url);
                Modellen.Children.Clear();
                return;
            }

            Bolletje.Fill = Kleur("HealthOkBrush", Brushes.LimeGreen);
            StatusText.Text = Teksten.Vul(
                this,
                "T_OllamaStatusFormaat",
                "Running · version {0} · {1} · {2} model(s) loaded",
                versie,
                url,
                geladen.Count);

            Modellen.Children.Clear();

            if (geladen.Count == 0)
            {
                Modellen.Children.Add(Regel(Teksten.Van(this, "T_OllamaGeenModel", "No model in memory right now."), gedempt: true));
                return;
            }

            foreach (var model in geladen)
            {
                Modellen.Children.Add(Regel(Beschrijf(model), gedempt: false));
            }
        }
        finally
        {
            _bezig = false;
        }
    }

    private TextBlock Regel(string tekst, bool gedempt) => new()
    {
        Text = tekst,
        Margin = new Thickness(0, 2, 0, 2),
        TextWrapping = TextWrapping.Wrap,
        FontSize = TryFindResource("FontSizeSmall") is double grootte ? grootte : 14,
        Foreground = Kleur(gedempt ? "TextMutedBrush" : "TextPrimaryBrush", Brushes.White),
    };

    /// <summary>Bijvoorbeeld: qwen2.5-coder:14b — 9,0 GB · 100% GPU · context 8192 · gaat eruit over 4 min.</summary>
    private string Beschrijf(Geladen model)
    {
        var delen = new List<string>
        {
            model.Groottebytes > 0 ? FormatteerGrootte(model.Groottebytes) : "?",
        };

        if (model.Groottebytes > 0)
        {
            var gpu = (int)Math.Round(100.0 * model.VramBytes / model.Groottebytes);
            delen.Add(gpu >= 100
                ? "100% GPU"
                : gpu <= 0 ? "100% CPU" : $"{gpu}% GPU / {100 - gpu}% CPU");
        }

        if (model.Context > 0)
        {
            delen.Add(Teksten.Vul(this, "T_OllamaContext", "context {0}", model.Context));
        }

        if (model.VerlooptOp is { } tot)
        {
            var rest = tot - DateTimeOffset.Now;
            delen.Add(rest.TotalSeconds <= 0
                ? Teksten.Van(this, "T_OllamaLaatstUit", "unloading")
                : Teksten.Vul(this, "T_OllamaNogFormaat", "unloads in {0}", Afgerond(rest)));
        }

        return $"{model.Naam} — {string.Join(" · ", delen)}";
    }

    private static string Afgerond(TimeSpan duur) =>
        duur.TotalMinutes >= 1 ? $"{(int)Math.Ceiling(duur.TotalMinutes)} min" : $"{(int)Math.Ceiling(duur.TotalSeconds)} s";

    private static string FormatteerGrootte(long bytes) =>
        (bytes / 1_073_741_824.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB";

    private static Geladen Lees(JsonElement model)
    {
        string Tekst(string naam) => model.TryGetProperty(naam, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : string.Empty;
        long Getal(string naam) => model.TryGetProperty(naam, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0;

        DateTimeOffset? verloopt = null;

        if (DateTimeOffset.TryParse(Tekst("expires_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var tijd) && tijd.Year > 2000)
        {
            verloopt = tijd;
        }

        var naam = Tekst("name");
        return new Geladen(naam.Length > 0 ? naam : Tekst("model"), Getal("size"), Getal("size_vram"), (int)Getal("context_length"), verloopt);
    }

    private sealed record Geladen(string Naam, long Groottebytes, long VramBytes, int Context, DateTimeOffset? VerlooptOp);
}
