using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Taal;
using Jabasoft.Base.AiBroker;
using Jabasoft.Base.Logging;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Setting-09.xaml - see that file for what it looks like.
///
/// Net als de Hermes-kaart (Setting08) bewaart deze niets in de app zelf:
/// een ander model gaat meteen naar de broker, die het in Goose' config zet.
/// Fouten komen terug als mislukt resultaat (zie GooseClient).
/// </summary>
public partial class Setting09 : UserControl
{
    private readonly GooseClient _goose = GooseClient.CreateDefault();

    private GooseStatus _status = GooseStatus.Leeg;

    private readonly IAiBrokerClient _broker = AiBrokerClient.CreateDefault();

    /// <summary>De AI-instellingen (LM Studio) - voor de modellen die in de modeltabel op "niet tonen" staan.</summary>
    private AiServerSettings? _lmStudio;

    /// <summary>Aan terwijl de kaart zichzelf invult - dan mag niets een opslag uitlokken (zie Setting06._laden).</summary>
    private bool _laden;

    /// <summary>Eén keuze in de modellijst.</summary>
    private sealed record GooseKeuze(string Naam, string Info);

    public Setting09()
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

    /// <summary>Waar Goose staat (de standaard installatie per gebruiker).</summary>
    private static string GooseExe => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Goose", "Goose.exe");

    /// <summary>Haalt de stand op bij de broker - elke keer dat de kaart in beeld komt, want Goose kan intussen zelf iets gewijzigd hebben.</summary>
    private async Task LaadAsync()
    {
        Meld(Teksten.Van(this, "T_AiLaden", "Loading…"));

        var instellingenTaak = _broker.GetSettingsAsync(CancellationToken.None);
        var result = await _goose.GetAsync(CancellationToken.None);
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

    private void Toon(GooseStatus status)
    {
        _status = status;
        _laden = true;
        try
        {
            StatusRegel.Text = !status.Geinstalleerd
                ? Teksten.Van(this, "T_GooseNietGeinstalleerd", "Goose is not installed on this machine.")
                : Teksten.Vul(
                    this,
                    "T_GooseStatusFormaat",
                    "Installed · {0} · provider {1} · {2}",
                    status.Draait ? Teksten.Van(this, "T_HermesDraait", "running") : Teksten.Van(this, "T_HermesDraaitNiet", "not running"),
                    status.Provider.Length > 0 ? status.Provider : "-",
                    status.ConfigMap);

            ZetBewerkbaar(status.Geinstalleerd);
            VulModellen();
            foreach (var knop in GrensKnoppen())
            {
                knop.IsChecked = GrensVan(knop) == status.MaxContext;
            }

            ToonModelDoel();
        }
        finally
        {
            _laden = false;
        }
    }

    /// <summary>
    /// De modellen van LM Studio (geen embeddings - die filtert de broker al),
    /// met genoeg context bovenaan. Een ingesteld model dat niet (meer) op LM
    /// Studio staat blijft zichtbaar, anders zou het openen van de kaart het
    /// stilletjes vervangen.
    /// </summary>
    private void VulModellen()
    {
        // Alleen wat zinvol is als hoofdmodel van een agent: genoeg context en een Agent-score van minstens 3.
        // Onze vastgestelde keuze (Aanbevelingen) bovenaan, dan de beste.
        var keuzes = _status.LokaleModellen
            .Where(model => model.MaxContext >= HermesStatus.MinimaleContext && Keuze(model).Taken.Agent >= 3)
            // Op "niet tonen" in de modeltabel: weg, behalve het model dat Goose nu gebruikt (dat komt hieronder terug).
            .Where(model => _lmStudio?.IsVerborgen(model.Naam) != true || string.Equals(model.Naam, _status.Model, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(model => Aanbevelingen.Voor(model.Naam).Count > 0)
            .ThenByDescending(model => Keuze(model).Taken.Agent)
            .ThenBy(model => model.Naam, StringComparer.OrdinalIgnoreCase)
            .Select(model => new GooseKeuze(model.Naam, Info(model)))
            .ToList();

        var actiefJabasoft = string.Equals(_status.Provider, "jabasoft", StringComparison.OrdinalIgnoreCase);
        if (actiefJabasoft && _status.Model.Length > 0 &&
            keuzes.All(keuze => !string.Equals(keuze.Naam, _status.Model, StringComparison.OrdinalIgnoreCase)))
        {
            keuzes.Insert(0, new GooseKeuze(_status.Model, Teksten.Van(this, "T_HermesNietOpServer", "not on LM Studio")));
        }

        ModelPicker.ItemsSource = keuzes;
        ModelPicker.SelectedItem = actiefJabasoft
            ? keuzes.FirstOrDefault(keuze => string.Equals(keuze.Naam, _status.Model, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private Modelkeuze Keuze(HermesLokaalModel model) =>
        Modelteksten.Keuze(this, model.Naam, kanBeelden: model.KanBeelden, maxContext: model.MaxContext);

    private string Info(HermesLokaalModel model)
    {
        var context = model.MaxContext >= HermesStatus.MinimaleContext
            ? Teksten.Vul(this, "T_HermesContextFormaat", "{0:N0} tokens context", model.MaxContext)
            : Teksten.Vul(this, "T_GooseContextTeKleinFormaat", "{0:N0} tokens context — too small for an agent (min. 64,000)", model.MaxContext);

        return Keuze(model).DoelTekst is { Length: > 0 } doel ? context + " · " + doel : context;
    }

    private void ToonModelDoel()
    {
        var naam = (ModelPicker.SelectedItem as GooseKeuze)?.Naam;
        var gekozen = _status.LokaleModellen.FirstOrDefault(model => string.Equals(model.Naam, naam, StringComparison.OrdinalIgnoreCase));
        ToonGrensUitleg(gekozen);
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
    /// Onder de grens: hoe het meegroeien werkt (of waar het model zelf stopt) en met hoeveel context het model
    /// nu geladen staat - zo zie je of het al meegegroeid is.
    /// </summary>
    private void ToonGrensUitleg(HermesLokaalModel? gekozen)
    {
        var uitleg = gekozen is not null && gekozen.MaxContext >= HermesStatus.MinimaleContext && gekozen.MaxContext < _status.MaxContext
            ? Teksten.Vul(this, "T_HermesGrensModelMaxFormaat", "Grows with the conversation, starting at 65,536 tokens. {0} handles at most {1:N0} tokens, so it stops there.", gekozen.Naam, gekozen.MaxContext)
            : Teksten.Van(this, "T_GooseGrensUitleg", "Grows with the conversation, starting at 65,536 tokens. Each step means a short wait while the model reloads. The broker uses the new limit right away; Goose itself (which summarises at the limit) after a restart.");

        var nu = _status.GeladenContext > 0
            ? Teksten.Vul(this, "T_GooseGeladenFormaat", "Loaded now with {0:N0} tokens context.", _status.GeladenContext)
            : Teksten.Van(this, "T_GooseNietGeladen", "Not loaded right now.");

        GrensUitleg.Text = uitleg + "\n" + nu;
    }

    private IEnumerable<RadioButton> GrensKnoppen() => [Grens96, Grens128, Grens192, Grens256];

    private static int GrensVan(RadioButton knop) =>
        int.TryParse(knop.Tag as string, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var waarde) ? waarde : 0;

    /// <summary>De gekozen grens, of 0 (= de broker houdt wat er staat) als er geen knop aan staat.</summary>
    private int GekozenGrens() => GrensKnoppen().FirstOrDefault(knop => knop.IsChecked == true) is { } gekozen ? GrensVan(gekozen) : 0;

    private void ZetBewerkbaar(bool aan)
    {
        foreach (var knop in GrensKnoppen())
        {
            knop.IsEnabled = aan;
        }

        ModelPicker.IsEnabled = aan;
        OpenGooseButton.IsEnabled = aan && File.Exists(GooseExe);
    }

    private void Meld(string tekst) => MeldingTekst.Text = tekst;

    private async void OnTaalGewisseld(object? sender, EventArgs e) => await LaadAsync();

    private async void Model_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

    /// <summary>Stuurt model en grens naar de broker. Alles wat opslaat komt hierlangs.</summary>
    private async Task BewaarAsync()
    {
        if (ModelPicker.SelectedItem is not GooseKeuze gekozen)
        {
            return;
        }

        var result = await _goose.SaveAsync(new GooseInstelling(gekozen.Naam, GekozenGrens()), CancellationToken.None);
        if (!result.Success)
        {
            Meld(Teksten.Vul(this, "T_AiNietBewaardFormaat", "Not saved: {0}", result.ErrorMessage));
            return;
        }

        Toon(result.Status);
        ActivityLog.Shared.Add("app", $"Goose ingesteld: '{gekozen.Naam}' (grens {result.Status.MaxContext:N0})");
        Meld(Teksten.Van(this, "T_GooseBewaard", "Saved - restart Goose to use it."));
    }

    /// <summary>
    /// Het muiswiel boven een dichte keuzelijst wisselt niet van model maar scrolt de pagina: zo'n
    /// onbedoelde reeks wissels zette Goose eerder op een klein model. Open lijst: gewoon scrollen in de lijst.
    /// </summary>
    private void ModelPicker_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (ModelPicker.IsDropDownOpen)
        {
            return;
        }

        e.Handled = true;
        if (System.Windows.Media.VisualTreeHelper.GetParent(ModelPicker) is UIElement ouder)
        {
            ouder.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = ModelPicker,
            });
        }
    }

    private async void Vernieuw_Click(object sender, RoutedEventArgs e) => await LaadAsync();

    private void OpenGoose_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(GooseExe) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Meld(Teksten.Vul(this, "T_GooseOpenFoutFormaat", "Could not open Goose: {0}", ex.Message));
        }
    }
}
