using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Jabasoft.App.Taal;
using Jabasoft.Base.AiBroker;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Setting-10.xaml - see that file for what it looks like.
///
/// Haalt drie dingen bij de broker op: de AI-instellingen (rollen, sterren,
/// notities, modellijst van de actieve server), Hermes en Goose (welk model
/// ze gebruiken, plus van LM Studio de context en of een model beelden leest).
/// Mislukt er een, dan blijft de tabel staan met wat er wel is.
/// </summary>
public partial class Setting10 : UserControl
{
    private static readonly Taak[] TaakKolommen = [Taak.Chat, Taak.Code, Taak.Controle, Taak.Beeld, Taak.Zoeken, Taak.Agent];

    private readonly IAiBrokerClient _broker = AiBrokerClient.CreateDefault();
    private readonly HermesClient _hermes = HermesClient.CreateDefault();
    private readonly GooseClient _goose = GooseClient.CreateDefault();

    /// <summary>De kleuren die de gebruiker vroeg: groen = samen vastgesteld, geel = eigen favoriet.</summary>
    private static readonly Brush Groen = Bevroren(Color.FromRgb(0x3F, 0xB9, 0x50));
    private static readonly Brush Geel = Bevroren(Color.FromRgb(0xE3, 0xB3, 0x41));

    /// <summary>Wat de broker nu als AI-instelling heeft - nodig om een favoriet erbij te zetten.</summary>
    private AiSettings _settings = AiSettings.Default;

    private static Brush Bevroren(Color kleur)
    {
        var brush = new SolidColorBrush(kleur);
        brush.Freeze();
        return brush;
    }

    public Setting10()
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

    private async Task LaadAsync()
    {
        MeldingTekst.Text = Teksten.Van(this, "T_AiModellenLaden", "Loading models…");

        var instellingenTaak = _broker.GetSettingsAsync(CancellationToken.None);
        var lijstTaak = _broker.ListConfiguredModelsAsync(CancellationToken.None);
        var hermesTaak = _hermes.GetAsync(CancellationToken.None);
        var gooseTaak = _goose.GetAsync(CancellationToken.None);
        await Task.WhenAll(instellingenTaak, lijstTaak, hermesTaak, gooseTaak);

        var instellingen = instellingenTaak.Result;
        _settings = instellingen.Settings;
        var hermes = hermesTaak.Result.Status;
        var goose = gooseTaak.Result.Status;

        // LM Studio-gegevens (context, beelden): van Goose of Hermes, dezelfde lijst.
        var lmStudio = (goose.LokaleModellen.Count > 0 ? goose.LokaleModellen : hermes.LokaleModellen)
            .ToDictionary(model => model.Naam, StringComparer.OrdinalIgnoreCase);

        var actief = instellingen.Settings.Active;
        var alleNamen = lijstTaak.Result.Models
            .Concat(lmStudio.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var aantalVerborgen = alleNamen.Count(actief.IsVerborgen);

        var namen = alleNamen
            // Verborgen modellen alleen met het filter "verborgen tonen" aan - en dan onderaan.
            .Where(naam => VerborgenTonen.IsChecked == true || !actief.IsVerborgen(naam))
            .OrderBy(naam => actief.IsVerborgen(naam))
            // Eerst wat we samen vastgesteld hebben, dan de favorieten, dan de rest - elk op naam.
            .ThenByDescending(naam => Aanbevelingen.Voor(naam).Count > 0)
            .ThenByDescending(naam => instellingen.Settings.Active.IsFavoriet(naam))
            .ThenBy(naam => naam, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var gebruik = Gebruik(instellingen.Settings, hermes, goose);

        Tabel.Children.Clear();
        Tabel.Children.Add(BouwKop());

        foreach (var naam in namen)
        {
            lmStudio.TryGetValue(naam, out var lokaal);
            Tabel.Children.Add(BouwRij(naam, lokaal, instellingen.Settings.Active, gebruik.TryGetValue(naam, out var rollen) ? rollen : []));
        }

        MeldingTekst.Text = !instellingen.Success
            ? Teksten.Vul(this, "T_AiBrokerWegFormaat", "Broker not reachable: {0}", instellingen.ErrorMessage)
            : Teksten.Vul(this, "T_ModeltabelAantalVerborgenFormaat", "{0} models, {1} of them hidden", alleNamen.Count, aantalVerborgen);
    }

    /// <summary>Per modelnaam: in welke rollen en agents het nu gebruikt wordt.</summary>
    private Dictionary<string, List<string>> Gebruik(AiSettings instellingen, HermesStatus hermes, GooseStatus goose)
    {
        var gebruik = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void Voeg(string model, string rol)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return;
            }

            if (!gebruik.TryGetValue(model, out var lijst))
            {
                lijst = [];
                gebruik[model] = lijst;
            }

            lijst.Add(rol);
        }

        var actief = instellingen.Active;
        Voeg(actief.ChatModel, Modelteksten.TaakNaam(this, Taak.Chat));
        Voeg(actief.CodeModel, Modelteksten.TaakNaam(this, Taak.Code));
        Voeg(actief.ControleModel, Modelteksten.TaakNaam(this, Taak.Controle));
        Voeg(actief.BeeldModel, Modelteksten.TaakNaam(this, Taak.Beeld));
        Voeg(actief.EmbedModel, Modelteksten.TaakNaam(this, Taak.Zoeken));

        if (hermes.Geinstalleerd)
        {
            if (string.Equals(hermes.Provider, "lmstudio", StringComparison.OrdinalIgnoreCase))
            {
                Voeg(hermes.Model, "Hermes");
            }

            Voeg(hermes.LokaalModel, Teksten.Van(this, "T_ModeltabelHermesLokaal", "Hermes (side tasks)"));
        }

        if (goose.Geinstalleerd && string.Equals(goose.Provider, "jabasoft", StringComparison.OrdinalIgnoreCase))
        {
            Voeg(goose.Model, "Goose");
        }

        return gebruik;
    }

    /// <summary>Dezelfde kolommen voor kop en rijen - zo staan ze recht onder elkaar.</summary>
    private static Grid NieuweRij()
    {
        var rij = new Grid();
        foreach (var _ in Onderdelen.Alle)
        {
            rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        }

        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        foreach (var _ in TaakKolommen)
        {
            rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        }

        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        return rij;
    }

    private UIElement BouwKop()
    {
        var kop = NieuweRij();
        kop.Margin = new Thickness(0, 0, 0, 8);

        var sterTip = Teksten.Van(this, "T_ModeltabelSterTip", "Green: the best choice we determined together. Yellow: your own favourite - click to set or remove.");
        var koppen = new List<(string Tekst, string? Tip, bool Midden)>();
        koppen.AddRange(Onderdelen.Alle.Select(onderdeel => (OnderdeelAfkorting(onderdeel), (string?)(onderdeel + "\n" + sterTip), true)));
        koppen.AddRange(new List<(string Tekst, string? Tip, bool Midden)>
        {
            (Teksten.Van(this, "T_ModeltabelTonen", "Show"), Teksten.Van(this, "T_ModeltabelTonenTip", "Off = not offered in the model lists (AI, Hermes and Goose cards, and in Hermes and Goose themselves). A model that is in use stays where it is used."), true),
            (Teksten.Van(this, "T_ModeltabelModel", "Model"), null, false),
            (Teksten.Van(this, "T_AiModellenKwaliteitKop", "Quality"), Teksten.Van(this, "T_AiSterrenTip", "Quality estimate"), false),
            (Teksten.Van(this, "T_ModeltabelContext", "Context"), Teksten.Van(this, "T_ModeltabelContextTip", "Largest context according to LM Studio"), false),
            (Teksten.Van(this, "T_ModeltabelBeelden", "Images"), Teksten.Van(this, "T_ModeltabelBeeldenTip", "Can read images"), true),
            (Teksten.Van(this, "T_ModeltabelDenkt", "Thinks"), Teksten.Van(this, "T_ModeltabelDenktTip", "Thinks before answering (reasoning) - slower, stronger at checking"), true),
        });

        koppen.AddRange(TaakKolommen.Select(taak => (KorteTaakNaam(taak), (string?)TaakTip(taak), true)));
        koppen.Add((Teksten.Van(this, "T_ModeltabelGebruik", "In use for"), null, false));

        for (var i = 0; i < koppen.Count; i++)
        {
            var (tekst, tip, midden) = koppen[i];
            var blok = Cel(tekst, Kleur("TextMutedBrush", Brushes.Gray), midden);
            blok.FontWeight = FontWeights.SemiBold;
            blok.FontSize = KleinLettertype();
            if (!string.IsNullOrEmpty(tip))
            {
                blok.ToolTip = tip;
            }

            Grid.SetColumn(blok, i);
            kop.Children.Add(blok);
        }

        return kop;
    }

    private UIElement BouwRij(string naam, HermesLokaalModel? lokaal, AiServerSettings actief, List<string> rollen)
    {
        var keuze = Modelteksten.Keuze(this, naam, actief.SterrenVoor(naam), lokaal?.KanBeelden, lokaal?.MaxContext ?? 0);

        var tekst = Kleur("TextPrimaryBrush", Brushes.White);
        var gedempt = Kleur("TextMutedBrush", Brushes.Gray);
        var accent = Kleur("AccentBrush", Brushes.Orange);

        var rij = NieuweRij();
        rij.Margin = new Thickness(0, 0, 0, 10);

        var kolom = 0;
        foreach (var onderdeel in Onderdelen.Alle)
        {
            Zet(rij, kolom++, BouwSter(naam, onderdeel, actief));
        }

        var verborgen = actief.IsVerborgen(naam);
        var tonen = new CheckBox
        {
            IsChecked = !verborgen,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
        };
        AutomationProperties.SetName(tonen, "Tonen " + naam);
        tonen.Click += async (_, _) => await WisselVerborgenAsync(naam, tonen.IsChecked != true);
        Zet(rij, kolom++, tonen);

        if (verborgen)
        {
            rij.Opacity = 0.45;
        }

        // Model: naam, grootte · soort, en waar het voor bedoeld is. De omschrijving (eigen notitie of de
        // automatische) als tooltip - te lang voor in de tabel.
        var naamBlok = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        naamBlok.Children.Add(new TextBlock { Text = keuze.Naam, Foreground = rollen.Count > 0 ? accent : tekst, TextTrimming = TextTrimming.CharacterEllipsis });
        naamBlok.Children.Add(new TextBlock { Text = keuze.Info, Foreground = gedempt, FontSize = KleinLettertype() });
        if (keuze.DoelTekst.Length > 0)
        {
            naamBlok.Children.Add(new TextBlock
            {
                Text = Teksten.Vul(this, "T_ModelDoelFormaat", "Intended for: {0}", keuze.DoelTekst),
                Foreground = tekst,
                FontSize = KleinLettertype(),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var omschrijving = actief.OmschrijvingVoor(naam) is { Length: > 0 } eigen ? eigen : Modelkeuze.AutomatischeOmschrijving(naam);
        if (omschrijving.Length > 0)
        {
            naamBlok.ToolTip = new TextBlock { Text = omschrijving, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
        }

        Zet(rij, kolom++, naamBlok);
        Zet(rij, kolom++, Cel(keuze.SterrenTekst, accent, midden: false));
        Zet(rij, kolom++, Cel(
            keuze.MaxContext > 0 ? KorteContext(keuze.MaxContext) : "?",
            keuze.MaxContext is > 0 and < HermesStatus.MinimaleContext ? gedempt : tekst,
            midden: false));
        Zet(rij, kolom++, Cel(keuze.KanBeelden ? "✓" : "–", keuze.KanBeelden ? accent : gedempt, midden: true));
        Zet(rij, kolom++, Cel(keuze.Denkt ? "✓" : "–", keuze.Denkt ? accent : gedempt, midden: true));

        foreach (var taak in TaakKolommen)
        {
            var score = keuze.Score(taak);
            var cel = Cel(score.ToString(CultureInfo.CurrentCulture), score >= 5 ? accent : score == 0 ? gedempt : tekst, midden: true);
            cel.FontWeight = keuze.BedoeldVoor.Contains(taak) ? FontWeights.Bold : FontWeights.Normal;
            Zet(rij, kolom++, cel);
        }

        var gebruikCel = Cel(rollen.Count > 0 ? string.Join(", ", rollen) : "–", rollen.Count > 0 ? tekst : gedempt, midden: false);
        gebruikCel.TextWrapping = TextWrapping.Wrap;
        Zet(rij, kolom, gebruikCel);

        return rij;
    }

    /// <summary>Korte kop boven een sterkolom - de volle naam staat in de tooltip.</summary>
    private static string OnderdeelAfkorting(string onderdeel) => onderdeel switch
    {
        Onderdelen.Goose => "Go",
        Onderdelen.Hermes => "He",
        Onderdelen.LocalAiStudio => "LA",
        Onderdelen.ImageFunn => "IF",
        Onderdelen.TabStudio => "TS",
        _ => onderdeel[..Math.Min(2, onderdeel.Length)],
    };

    /// <summary>
    /// Eén sterkolom (per onderdeel): groen als we dit model voor dit onderdeel
    /// samen als beste keuze vastgesteld hebben (waarom in de tooltip), anders
    /// geel als het je eigen favoriet voor dit onderdeel is, anders leeg (☆).
    /// Klikken zet of haalt de eigen favoriet - ook onder een groene ster.
    /// </summary>
    private UIElement BouwSter(string naam, string onderdeel, AiServerSettings actief)
    {
        var aanbevelingen = Aanbevelingen.Voor(naam, onderdeel);
        var favoriet = actief.IsFavoriet(naam, onderdeel);

        var tip = favoriet
            ? Teksten.Van(this, "T_ModeltabelFavorietUit", "Your favourite - click to remove")
            : Teksten.Van(this, "T_ModeltabelFavorietAan", "Click to mark as favourite");
        if (aanbevelingen.Count > 0)
        {
            tip = string.Join("\n\n", aanbevelingen.Select(a =>
                Teksten.Vul(this, "T_ModeltabelOnzeKeuzeFormaat", "Our choice for {0}: {1}", Modelteksten.TaakNaam(this, a.Taak), a.Reden))) + "\n\n" + tip;
        }

        // Groen én eigen favoriet: twee kleine sterren naast elkaar, anders zie je de gele klik niet terug.
        var beide = aanbevelingen.Count > 0 && favoriet;
        var ster = new TextBlock
        {
            FontSize = beide ? 12 : 18,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = beide ? new Thickness(0, 4, 0, 0) : new Thickness(0),
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = new TextBlock { Text = onderdeel + "\n\n" + tip, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
        };

        if (beide)
        {
            ster.Inlines.Add(new System.Windows.Documents.Run("★") { Foreground = Groen });
            ster.Inlines.Add(new System.Windows.Documents.Run("★") { Foreground = Geel });
        }
        else
        {
            ster.Text = aanbevelingen.Count > 0 || favoriet ? "★" : "☆";
            ster.Foreground = aanbevelingen.Count > 0 ? Groen : favoriet ? Geel : Kleur("TextMutedBrush", Brushes.Gray);
        }

        AutomationProperties.SetName(ster, $"Favoriet {naam} voor {onderdeel}");
        ster.MouseLeftButtonUp += async (_, _) => await WisselFavorietAsync(naam, onderdeel);
        return ster;
    }

    /// <summary>Zet een model op "niet tonen" in de keuzelijsten, of weer terug, via de broker (ai.json - geldt voor alle apps).</summary>
    private async Task WisselVerborgenAsync(string naam, bool verborgen)
    {
        var result = await _broker.SaveSettingsAsync(_settings.With(_settings.Provider, _settings.Active.MetVerborgen(naam, verborgen)), CancellationToken.None);
        if (!result.Success)
        {
            MeldingTekst.Text = Teksten.Vul(this, "T_AiNietBewaardFormaat", "Not saved: {0}", result.ErrorMessage);
            return;
        }

        await LaadAsync();
    }

    /// <summary>Zet een model voor één onderdeel bij de favorieten of haalt het eraf, via de broker (ai.json).</summary>
    private async Task WisselFavorietAsync(string naam, string onderdeel)
    {
        var actief = _settings.Active;
        var bijgewerkt = actief.MetFavoriet(naam, onderdeel, !actief.IsFavoriet(naam, onderdeel));

        var result = await _broker.SaveSettingsAsync(_settings.With(_settings.Provider, bijgewerkt), CancellationToken.None);
        if (!result.Success)
        {
            MeldingTekst.Text = Teksten.Vul(this, "T_AiNietBewaardFormaat", "Not saved: {0}", result.ErrorMessage);
            return;
        }

        await LaadAsync();
    }

    private static void Zet(Grid rij, int kolom, UIElement element)
    {
        Grid.SetColumn(element, kolom);
        rij.Children.Add(element);
    }

    private static TextBlock Cel(string tekst, Brush kleur, bool midden) => new()
    {
        Text = tekst,
        Foreground = kleur,
        TextAlignment = midden ? TextAlignment.Center : TextAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    /// <summary>"128k" in plaats van "131.072" - in een smalle kolom leest dat beter.</summary>
    private static string KorteContext(int tokens) =>
        (tokens / 1024).ToString(CultureInfo.CurrentCulture) + "k";

    /// <summary>De kolomkoppen voor de taken: kort, met de lange naam als tooltip.</summary>
    private string KorteTaakNaam(Taak taak) => taak switch
    {
        Taak.Controle => Teksten.Van(this, "T_AiTaakControle", "Ctrl"),
        _ => Modelteksten.TaakNaam(this, taak),
    };

    private string TaakTip(Taak taak) => taak switch
    {
        Taak.Chat => Teksten.Van(this, "T_AiTaakChatTip", "Chat"),
        Taak.Code => Teksten.Van(this, "T_AiTaakCodeTip", "Code"),
        Taak.Controle => Teksten.Van(this, "T_AiTaakControleTip", "Check"),
        Taak.Beeld => Teksten.Van(this, "T_AiTaakBeeldTip", "Image"),
        Taak.Zoeken => Teksten.Van(this, "T_AiTaakZoekenTip", "Search"),
        _ => Teksten.Van(this, "T_AiTaakAgentTip", "Estimated fit as the main model of an agent (Hermes, Goose), 0-5"),
    };

    private Brush Kleur(string sleutel, Brush terugval) => TryFindResource(sleutel) as Brush ?? terugval;

    private double KleinLettertype() => TryFindResource("FontSizeSmall") is double klein ? klein : 12;

    private async void OnTaalGewisseld(object? sender, EventArgs e) => await LaadAsync();

    private async void Vernieuw_Click(object sender, RoutedEventArgs e) => await LaadAsync();

    private async void VerborgenTonen_Click(object sender, RoutedEventArgs e) => await LaadAsync();
}
