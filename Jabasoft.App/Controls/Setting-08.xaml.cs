using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Jabasoft.App.Taal;
using Jabasoft.Base.AiBroker;
using Jabasoft.Base.Logging;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Setting-08.xaml - see that file for what it looks like.
///
/// Net als de AI-kaart (Setting06) bewaart deze niets in de app zelf: elke
/// wijziging gaat meteen naar de broker, die hem in Hermes' config.yaml zet.
/// Fouten komen terug als mislukt resultaat (zie HermesClient), dus ook hier
/// geen try/catch - alleen een melding onderaan de kaart.
/// </summary>
public partial class Setting08 : UserControl
{
    /// <summary>Het Store-pakket van Hermes Agent (Nous Research) - om het te kunnen openen.</summary>
    private const string HermesStartAdres = "shell:AppsFolder\\NousResearchInc.HermesAgent_sznbd6dv1j3v4!HermesBundled";

    private readonly HermesClient _hermes = HermesClient.CreateDefault();

    private HermesStatus _status = HermesStatus.Leeg;

    private readonly IAiBrokerClient _broker = AiBrokerClient.CreateDefault();

    /// <summary>De AI-instellingen (LM Studio) - voor de modellen die in de modeltabel op "niet tonen" staan.</summary>
    private AiServerSettings? _lmStudio;

    /// <summary>Hoort dit model in de keuzelijst? Niet als het verborgen is, tenzij het nu ingesteld staat.</summary>
    private bool Tonen(string naam, string ingesteld) =>
        _lmStudio?.IsVerborgen(naam) != true || string.Equals(naam, ingesteld, StringComparison.OrdinalIgnoreCase);

    /// <summary>Aan terwijl de kaart zichzelf invult - dan mag niets een opslag uitlokken (zie Setting06._laden).</summary>
    private bool _laden;

    /// <summary>Eén keuze in de lijst met lokale modellen. Leeg <see cref="Naam"/> = geen lokaal model.</summary>
    private sealed record LokaalKeuze(string Naam, string Info);

    public Setting08()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            LanguageManager.Changed -= OnTaalGewisseld;
            LanguageManager.Changed += OnTaalGewisseld;
            await LaadAsync();
        };

        Unloaded += (_, _) => LanguageManager.Changed -= OnTaalGewisseld;
    }

    /// <summary>Haalt de stand op bij de broker - elke keer dat de kaart in beeld komt, want Hermes kan intussen zelf iets gewijzigd hebben.</summary>
    private async Task LaadAsync()
    {
        Meld(Teksten.Van(this, "T_AiLaden", "Loading…"));

        var instellingenTaak = _broker.GetSettingsAsync(CancellationToken.None);
        var result = await _hermes.GetAsync(CancellationToken.None);
        var instellingen = await instellingenTaak;
        _lmStudio = instellingen.Success ? instellingen.Settings.LmStudio : null;
        if (!result.Success)
        {
            Meld(Teksten.Vul(this, "T_AiBrokerWegFormaat", "Broker not reachable: {0}", result.ErrorMessage));
            ZetBewerkbaar(false);
            return;
        }

        Toon(result.Status);
        Meld(string.Empty);
    }

    /// <summary>Zet een status van de broker op de kaart.</summary>
    private void Toon(HermesStatus status)
    {
        _status = status;
        _laden = true;
        try
        {
            ToonStatusRegel();
            ZetBewerkbaar(status.Geinstalleerd);

            var provider = status.Provider.ToLowerInvariant();
            foreach (var knop in ProviderKnoppen())
            {
                knop.IsChecked = string.Equals((string)knop.Tag, provider, StringComparison.OrdinalIgnoreCase);
            }

            VulModelSuggesties(provider);
            ModelPicker.Text = status.Model;

            ToonSleutel();
            ToonLokaalOfCloud(provider);

            McpAan.IsChecked = status.JabasoftMcp;
            McpUit.IsChecked = !status.JabasoftMcp;

            foreach (var knop in GrensKnoppen())
            {
                knop.IsChecked = GrensVan(knop) == status.MaxContext;
            }
        }
        finally
        {
            _laden = false;
        }
    }

    private void ToonStatusRegel()
    {
        if (!_status.Geinstalleerd)
        {
            StatusRegel.Text = Teksten.Van(this, "T_HermesNietGeinstalleerd", "Hermes Agent is not installed on this machine.");
            return;
        }

        StatusRegel.Text = Teksten.Vul(
            this,
            "T_HermesStatusFormaat",
            "Installed · gateway {0} · {1}",
            _status.GatewayDraait
                ? Teksten.Van(this, "T_HermesDraait", "running")
                : Teksten.Van(this, "T_HermesDraaitNiet", "not running"),
            _status.HermesMap);
    }

    private void ToonSleutel() => SleutelTekst.Text = _status.Sleutel switch
    {
        HermesSleutelStatus.Ingesteld => Teksten.Van(this, "T_HermesSleutelIngesteld", "Set (in Hermes)"),
        HermesSleutelStatus.Ontbreekt => Teksten.Van(this, "T_HermesSleutelOntbreekt", "Missing — enter it in Hermes itself"),
        HermesSleutelStatus.NietNodig => Teksten.Van(this, "T_HermesSleutelNietNodig", "Not needed (local)"),
        _ => Teksten.Van(this, "T_HermesSleutelOnbekend", "Unknown"),
    };

    /// <summary>De suggesties in de modellijst, per provider. Typen mag altijd - dit zijn alleen de voor de hand liggende.</summary>
    private void VulModelSuggesties(string provider)
    {
        string[] suggesties = provider switch
        {
            "anthropic" => ["claude-sonnet-5-5", "claude-opus-5-5", "claude-haiku-5-5", "claude-fable-5-1"],
            "openrouter" => ["anthropic/claude-sonnet-5-5", "anthropic/claude-opus-5-5", "anthropic/claude-haiku-5-5"],
            // Alleen wat Hermes als hoofdmodel kan gebruiken: genoeg context en geschikt als agent (zie
            // Modelkeuze.Taken.Agent) - beste eerst. Een eigen naam typen mag nog steeds.
            "lmstudio" => [.. _status.LokaleModellen
                .Where(model => model.MaxContext >= HermesStatus.MinimaleContext && Keuze(model).Taken.Agent >= 3)
                .Where(model => Tonen(model.Naam, _status.Model))
                .OrderByDescending(model => Aanbevelingen.Voor(model.Naam).Count > 0)
                .ThenByDescending(model => Keuze(model).Taken.Agent)
                .ThenBy(model => model.Naam, StringComparer.OrdinalIgnoreCase)
                .Select(model => model.Naam)],
            _ => [],
        };

        ModelPicker.ItemsSource = suggesties;
    }

    /// <summary>
    /// Vult de keuzelijst voor het lokale model. Bij een lokaal hoofdmodel
    /// (<paramref name="alleenBeelden"/>) gaat het alleen om afbeeldingen
    /// lezen, dus staan alleen de modellen erin die dat kunnen.
    /// </summary>
    private void VulLokaleModellen(bool alleenBeelden)
    {
        var keuzes = new List<LokaalKeuze>
        {
            new(string.Empty, alleenBeelden
                ? Teksten.Van(this, "T_HermesVisionGeen", "None — the main model reads images itself (if it can)")
                : Teksten.Van(this, "T_HermesLokaalGeen", "None — Hermes uses its main model for everything")),
        };

        keuzes.AddRange(_status.LokaleModellen.Where(model => (!alleenBeelden || model.KanBeelden) && Tonen(model.Naam, _status.LokaalModel)).Select(model => new LokaalKeuze(
            model.Naam,
            MetDoel(model, model.MaxContext >= HermesStatus.MinimaleContext
                ? Teksten.Vul(this, "T_HermesContextFormaat", "{0:N0} tokens context", model.MaxContext)
                : Teksten.Vul(this, "T_HermesContextTeKleinFormaat", "{0:N0} tokens context — too small for Hermes (min. 64,000)", model.MaxContext)))));

        // Een ingesteld model dat (nu) niet op LM Studio staat blijft
        // zichtbaar, anders zou het openen van de kaart het stilletjes wissen.
        // Staat het wel op LM Studio maar kan het geen beelden lezen, dan
        // hoort het niet in de lijst voor afbeeldingen (dan wordt het "Geen").
        if (_status.LokaalModel.Length > 0 &&
            keuzes.All(keuze => !string.Equals(keuze.Naam, _status.LokaalModel, StringComparison.OrdinalIgnoreCase)) &&
            _status.LokaleModellen.All(model => !string.Equals(model.Naam, _status.LokaalModel, StringComparison.OrdinalIgnoreCase)))
        {
            keuzes.Insert(1, new LokaalKeuze(_status.LokaalModel, Teksten.Van(this, "T_HermesNietOpServer", "not on LM Studio")));
        }

        LokaalModelPicker.ItemsSource = keuzes;
        LokaalModelPicker.SelectedItem = keuzes.FirstOrDefault(keuze => string.Equals(keuze.Naam, _status.LokaalModel, StringComparison.OrdinalIgnoreCase)) ?? keuzes[0];

        LokaalUitleg.Text = alleenBeelden
            ? Teksten.Van(this, "T_HermesVisionUitleg", "For a main model that cannot read images (such as gpt-oss). The broker swaps the two models in LM Studio, so an image takes a little longer.")
            : Teksten.Vul(
                this,
                "T_HermesLokaalUitlegFormaat",
                "For summarising, images (if the model can read them) and as a fallback when the main model is unavailable. Via {0} — load the model there with at least 64,000 tokens context.",
                _status.LokaalServerUrl);
    }

    /// <summary>De inschatting van een LM Studio-model, met wat LM Studio zelf weet (context, beelden).</summary>
    private Modelkeuze Keuze(HermesLokaalModel model) =>
        Modelteksten.Keuze(this, model.Naam, kanBeelden: model.KanBeelden, maxContext: model.MaxContext);

    /// <summary>De info achter een model in de keuzelijst, met erachter waar het voor bedoeld is.</summary>
    private string MetDoel(HermesLokaalModel model, string info) =>
        Keuze(model).DoelTekst is { Length: > 0 } doel ? info + " · " + doel : info;

    /// <summary>
    /// De regel onder het hoofdmodel: waar het gekozen LM Studio-model voor
    /// bedoeld is, en de korte omschrijving van zijn familie. Bij een
    /// cloudprovider (of een onbekend model) verborgen.
    /// </summary>
    private void ToonModelDoel(bool lokaal)
    {
        var gekozen = lokaal
            ? _status.LokaleModellen.FirstOrDefault(model => string.Equals(model.Naam, ModelPicker.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;

        if (gekozen is null)
        {
            ModelDoel.Visibility = Visibility.Collapsed;
            return;
        }

        var keuze = Keuze(gekozen);
        var omschrijving = Modelkeuze.AutomatischeOmschrijving(gekozen.Naam);
        ModelDoel.Text = Teksten.Vul(this, "T_ModelDoelFormaat", "Intended for: {0}", keuze.DoelTekst.Length > 0 ? keuze.DoelTekst : "-")
            + (omschrijving.Length > 0 ? "\n" + omschrijving : string.Empty);
        ModelDoel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// LM Studio als hoofdmodel: bijtaken en reserve hebben dan geen zin
    /// (alles is al lokaal), alleen een apart model voor afbeeldingen nog
    /// wel. Daaronder staat waar het naartoe gaat; een te klein model krijgt
    /// een waarschuwing.
    /// </summary>
    private void ToonLokaalOfCloud(string provider)
    {
        var lokaal = string.Equals(provider, "lmstudio", StringComparison.OrdinalIgnoreCase);
        LokaalHoofdUitleg.Visibility = lokaal ? Visibility.Visible : Visibility.Collapsed;
        GrensSectie.Visibility = lokaal ? Visibility.Visible : Visibility.Collapsed;
        LokaalGroepTitel.SetResourceReference(TextBlock.TextProperty, lokaal ? "T_HermesVisionGroep" : "T_HermesLokaalGroep");
        LokaalModelLabel.SetResourceReference(TextBlock.TextProperty, lokaal ? "T_HermesVisionModel" : "T_HermesLokaalModel");
        VulLokaleModellen(alleenBeelden: lokaal);
        ToonModelDoel(lokaal);

        if (!lokaal)
        {
            return;
        }

        var gekozen = _status.LokaleModellen.FirstOrDefault(model => string.Equals(model.Naam, ModelPicker.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        LokaalHoofdUitleg.Text = gekozen is not null && gekozen.MaxContext < HermesStatus.MinimaleContext
            ? Teksten.Vul(this, "T_HermesContextTeKleinFormaat", "{0:N0} tokens context — too small for Hermes (min. 64,000)", gekozen.MaxContext)
            : Teksten.Vul(this, "T_HermesViaBrokerFormaat", "Via the broker ({0}): in the same queue as the Jabasoft apps, and the broker loads the model in LM Studio itself.", _status.LokaalServerUrl);

        // Een grens boven wat het model aankan heeft geen zin: dat zegt de uitleg erbij.
        GrensUitleg.Text = gekozen is not null && gekozen.MaxContext >= HermesStatus.MinimaleContext && gekozen.MaxContext < _status.MaxContext
            ? Teksten.Vul(this, "T_HermesGrensModelMaxFormaat", "Grows with the conversation, starting at 65,536 tokens. {0} handles at most {1:N0} tokens, so it stops there.", gekozen.Naam, gekozen.MaxContext)
            : Teksten.Van(this, "T_HermesGrensUitleg", "Grows with the conversation, starting at 65,536 tokens. Each step means a short wait while the model reloads; higher costs more video memory, and if it does not fit the broker stays a step lower.");
    }

    private IEnumerable<RadioButton> ProviderKnoppen() => [ProviderAnthropic, ProviderOpenRouter, ProviderNous, ProviderLmStudio];

    private IEnumerable<RadioButton> GrensKnoppen() => [Grens96, Grens128, Grens192, Grens256];

    private static int GrensVan(RadioButton knop) =>
        int.TryParse(knop.Tag as string, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var waarde) ? waarde : 0;

    /// <summary>De gekozen grens, of 0 (= de broker houdt wat er staat) als er geen knop aan staat.</summary>
    private int GekozenGrens() => GrensKnoppen().FirstOrDefault(knop => knop.IsChecked == true) is { } gekozen ? GrensVan(gekozen) : 0;

    private string GekozenProvider() =>
        ProviderKnoppen().FirstOrDefault(knop => knop.IsChecked == true)?.Tag as string ?? _status.Provider;

    private void ZetBewerkbaar(bool aan)
    {
        foreach (var knop in ProviderKnoppen())
        {
            knop.IsEnabled = aan;
        }

        ModelPicker.IsEnabled = aan;
        LokaalModelPicker.IsEnabled = aan;
        McpAan.IsEnabled = aan;
        McpUit.IsEnabled = aan;
        foreach (var knop in GrensKnoppen())
        {
            knop.IsEnabled = aan;
        }

        OpenHermesButton.IsEnabled = aan;
    }

    /// <summary>Stuurt de stand van de kaart naar de broker. Alles wat opslaat komt hierlangs.</summary>
    private async Task BewaarAsync()
    {
        var instelling = new HermesInstelling(
            GekozenProvider(),
            ModelPicker.Text.Trim(),
            (LokaalModelPicker.SelectedItem as LokaalKeuze)?.Naam ?? string.Empty,
            McpAan.IsChecked == true,
            GekozenGrens());

        if (instelling.Model.Length == 0)
        {
            Meld(Teksten.Van(this, "T_HermesGeenModel", "Choose or type a model first."));
            return;
        }

        var result = await _hermes.SaveAsync(instelling, CancellationToken.None);
        if (!result.Success)
        {
            Meld(Teksten.Vul(this, "T_AiNietBewaardFormaat", "Not saved: {0}", result.ErrorMessage));
            return;
        }

        Toon(result.Status);
        ActivityLog.Shared.Add("app", $"Hermes ingesteld: {instelling.Provider} '{instelling.Model}', lokaal '{instelling.LokaalModel}', Jabasoft-tools {(instelling.JabasoftMcp ? "aan" : "uit")}");
        Meld(Teksten.Van(this, "T_HermesBewaard", "Saved — restart Hermes to use it."));
    }

    private void Meld(string tekst) => MeldingTekst.Text = tekst;

    private async void OnTaalGewisseld(object? sender, EventArgs e) => await LaadAsync();

    /// <summary>
    /// Andere provider: de suggesties gaan mee, en een model dat bij de
    /// vorige provider hoorde maakt plaats voor de eerste suggestie - anders
    /// zou bijvoorbeeld LM Studio gevraagd worden om "claude-sonnet-5-5".
    /// </summary>
    private async void Provider_Checked(object sender, RoutedEventArgs e)
    {
        if (_laden || !IsLoaded || sender is not RadioButton knop)
        {
            return;
        }

        var provider = (string)knop.Tag;

        _laden = true;
        try
        {
            VulModelSuggesties(provider);
            var suggesties = ModelPicker.ItemsSource as IEnumerable<string> ?? [];
            if (!suggesties.Contains(ModelPicker.Text, StringComparer.OrdinalIgnoreCase))
            {
                ModelPicker.Text = suggesties.FirstOrDefault() ?? string.Empty;
            }

            ToonLokaalOfCloud(provider);
        }
        finally
        {
            _laden = false;
        }

        await BewaarAsync();
    }

    private async void Model_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_laden || !IsLoaded || ModelPicker.SelectedItem is not string gekozen)
        {
            return;
        }

        ModelPicker.Text = gekozen;
        await ModelVerwerkenAsync();
    }

    private async void Model_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Focus die BINNEN de keuzelijst blijft (het tekstvak naar de
        // uitklaplijst) telt niet als wegklikken.
        if (ModelPicker.IsKeyboardFocusWithin)
        {
            return;
        }

        await ModelVerwerkenAsync();
    }

    /// <summary>Enter doet hetzelfde als wegklikken - anders moet je raden wanneer het model aankomt.</summary>
    private async void Model_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ModelVerwerkenAsync();
    }

    private async Task ModelVerwerkenAsync()
    {
        if (_laden || !IsLoaded || string.Equals(ModelPicker.Text.Trim(), _status.Model, StringComparison.Ordinal))
        {
            return;
        }

        await BewaarAsync();
    }

    private async void LokaalModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        await BewaarAsync();
    }

    private async void Mcp_Checked(object sender, RoutedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        await BewaarAsync();
    }

    private async void Grens_Checked(object sender, RoutedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        await BewaarAsync();
    }

    private async void Vernieuw_Click(object sender, RoutedEventArgs e) => await LaadAsync();

    private void OpenHermes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", HermesStartAdres) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Meld(Teksten.Vul(this, "T_HermesOpenFoutFormaat", "Could not open Hermes: {0}", ex.Message));
        }
    }
}
