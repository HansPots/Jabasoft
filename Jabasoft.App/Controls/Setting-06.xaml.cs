using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Jabasoft.Base.AiBroker;
using Jabasoft.App.Taal;
using Jabasoft.Base.Logging;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Setting-06.xaml - see that file for what it looks like.
///
/// Anders dan de andere kaarten zet deze niets in de ResourceDictionary van
/// de app: de waarde hoort niet bij DEZE applicatie maar bij de broker, en
/// geldt daarmee voor alle JabaSoft-applicaties. Elke wijziging gaat dus
/// meteen over de lijn naar de broker.
///
/// Elke aanroep vangt zijn eigen fouten al af (zie AiBrokerClient: alles
/// komt terug als een mislukt resultaat, niet als uitzondering), dus hier
/// staat nergens een try/catch - alleen een melding op de regel onderaan de
/// kaart.
/// </summary>
public partial class Setting06 : UserControl
{
    private readonly IAiBrokerClient _broker = AiBrokerClient.CreateDefault();

    /// <summary>Wat er volgens de broker nu ingesteld staat - het ijkpunt voor wat er gewijzigd is.</summary>
    private AiSettings _settings = AiSettings.Default;

    /// <summary>
    /// Aan terwijl de kaart zichzelf invult. Zonder deze grendel zou het
    /// vullen van de keuzelijsten en het aanvinken van de serverknop meteen
    /// weer een opslag uitlokken - en dan schrijf je de instelling over met
    /// wat je er net uit gelezen hebt.
    /// </summary>
    private bool _laden;

    /// <summary>Eén keuze in de sterren-combo: null = automatische schatting op grootte, anders 0-5 handmatig.</summary>
    private sealed record SterrenOptie(string Label, int? Waarde);

    /// <summary>Eén keuze in de denktijd-combo: 0 = geen apart limiet (de gewone wachttijd hierboven blijft gelden).</summary>
    private sealed record DenktijdOptie(string Label, int Seconden);

    private static readonly int[] DenktijdWaarden = [0, 15, 30, 60, 120, 300];

    public Setting06()
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

    /// <summary>
    /// Gaat af zodra er een nieuwe instelling bij de broker staat. Jabasoft
    /// laat daarop de gezondheidscontrole opnieuw lopen, zodat de pil in de
    /// footer meteen klopt met wat je net gekozen hebt.
    /// </summary>
    public event EventHandler? SettingsSaved;

    /// <summary>
    /// Haalt op wat er nu ingesteld staat en vult daarna de keuzelijsten.
    /// Loopt bij ELKE keer dat de kaart in beeld komt: de instelling kan
    /// intussen door een andere applicatie omgezet zijn.
    /// </summary>
    private async Task LaadAsync()
    {
        Meld(Teksten.Van(this, "T_AiLaden", "Loading…"));

        var result = await _broker.GetSettingsAsync(CancellationToken.None);
        _settings = result.Settings;

        _laden = true;
        try
        {
            ProviderLmStudio.IsChecked = _settings.Provider == AiProvider.LmStudio;
            ProviderOllama.IsChecked = _settings.Provider == AiProvider.Ollama;
            ServerUrlBox.Text = _settings.ActiveServerUrl;
            ContextRondesSlider.Value = _settings.MaxContextRondes;
            ChatTimeoutSlider.Value = _settings.ChatTimeoutSeconden / 60.0;
            OndergrensSlider.Value = _settings.MinimumSemanticScore * 100;
        }
        finally
        {
            _laden = false;
        }

        ToonContextRondes(ContextRondesSlider.Value);
        ToonChatTimeout(ChatTimeoutSlider.Value);
        ToonOndergrens(OndergrensSlider.Value);

        if (!result.Success)
        {
            Meld(Teksten.Vul(this, "T_AiBrokerWegFormaat", "Broker not reachable: {0}", result.ErrorMessage));
            return;
        }

        await VulModellenAsync();
    }

    /// <summary>De regel boven de schuifbalk, in de taal van nu.</summary>
    private void ToonOndergrens(double waarde)
    {
        if (OndergrensLabel is null)
        {
            return;
        }

        OndergrensLabel.Text = Teksten.Vul(this, "T_AiOndergrensFormaat", "Semantic search minimum — {0}%", (int)Math.Round(waarde));
    }

    /// <summary>De regel boven de contextronden-schuifbalk, in de taal van nu.</summary>
    private void ToonContextRondes(double waarde)
    {
        if (ContextRondesLabel is null)
        {
            return;
        }

        ContextRondesLabel.Text = Teksten.Vul(this, "T_AiContextRondesFormaat", "Automatically fetch more context — max {0} extra round(s)", (int)Math.Round(waarde));
    }

    /// <summary>De regel boven de wachttijd-schuifbalk, in de taal van nu.</summary>
    private void ToonChatTimeout(double waarde)
    {
        if (ChatTimeoutLabel is null)
        {
            return;
        }

        ChatTimeoutLabel.Text = Teksten.Vul(this, "T_AiChatTimeoutFormaat", "Response wait time — {0} minutes", (int)Math.Round(waarde));
    }

    /// <summary>
    /// Vraagt de broker welke modellen er op de ingestelde server AANWEZIG
    /// zijn en zet die in de keuzelijsten en het MODELLEN-blok.
    /// </summary>
    private async Task VulModellenAsync()
    {
        Meld(Teksten.Van(this, "T_AiModellenLaden", "Loading models…"));

        var lijst = await _broker.ListConfiguredModelsAsync(CancellationToken.None);
        var namen = lijst.Models.ToList();

        _laden = true;
        try
        {
            Vul(ChatModelPicker, namen, _settings.Active.ChatModel);
            Vul(EmbedModelPicker, namen, _settings.Active.EmbedModel);
            Vul(CodeModelPicker, namen, _settings.Active.CodeModel);
            Vul(ControleModelPicker, namen, _settings.Active.ControleModel);
            Vul(BeeldModelPicker, namen, _settings.Active.BeeldModel);

            VulModellenBlok(namen);
        }
        finally
        {
            _laden = false;
        }

        if (!lijst.Success)
        {
            Meld(Teksten.Vul(this, "T_AiGeenLijstFormaat", "No model list from {0}: {1}", _settings.ActiveServerUrl, lijst.ErrorMessage));
            return;
        }

        var ontbreekt = _settings.ConfiguredModels
            .Where(model => !namen.Contains(model, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Meld(ontbreekt.Count > 0
            ? Teksten.Vul(this, "T_AiOntbreektFormaat", "{0} models found — not present: {1}", namen.Count, string.Join(", ", ontbreekt))
            : Teksten.Vul(this, "T_AiGevondenFormaat", "{0} models found on {1}", namen.Count, _settings.ActiveServerUrl));
    }

    /// <summary>
    /// Zet de lijst in een keuzelijst en kiest wat er ingesteld staat. Een
    /// ingestelde naam die NIET in de lijst voorkomt wordt er bovenaan bij
    /// gezet: anders zou het openen van deze kaart je instelling stilletjes
    /// leegmaken, terwijl het juist de bedoeling is dat je ziet wat er mist.
    /// </summary>
    private void Vul(ComboBox keuzelijst, List<string> namen, string ingesteld)
    {
        var items = new List<string>(namen);

        if (!string.IsNullOrWhiteSpace(ingesteld) && !items.Contains(ingesteld, StringComparer.OrdinalIgnoreCase))
        {
            items.Insert(0, ingesteld);
        }

        var keuzes = items.Select(naam => Modelkeuze.Van(naam, SoortNaam, _settings.Active.SterrenVoor(naam))).ToList();

        keuzelijst.ItemsSource = keuzes;
        keuzelijst.SelectedItem = keuzes.FirstOrDefault(keuze => string.Equals(keuze.Naam, ingesteld, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Bouwt de items van een modelkeuzelijst opnieuw op (na een sterrenwijziging elders) en houdt de keuze vast.</summary>
    private void HerbouwKeuzelijst(ComboBox keuzelijst, string ingesteld)
    {
        if (keuzelijst.ItemsSource is not IEnumerable<Modelkeuze> huidige)
        {
            return;
        }

        var keuzes = huidige.Select(keuze => Modelkeuze.Van(keuze.Naam, SoortNaam, _settings.Active.SterrenVoor(keuze.Naam))).ToList();
        keuzelijst.ItemsSource = keuzes;
        keuzelijst.SelectedItem = keuzes.FirstOrDefault(keuze => string.Equals(keuze.Naam, ingesteld, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>De soort van een model in de taal van nu.</summary>
    private string SoortNaam(Modelsoort soort) => soort switch
    {
        Modelsoort.Code => Teksten.Van(this, "T_AiSoortCode", "code"),
        Modelsoort.Embedding => Teksten.Van(this, "T_AiSoortEmbedding", "embedding"),
        _ => Teksten.Van(this, "T_AiSoortAlgemeen", "general"),
    };

    private static string Gekozen(ComboBox keuzelijst) => (keuzelijst.SelectedItem as Modelkeuze)?.Naam ?? string.Empty;

    // ------------------------------------------------------------------
    // MODELLEN-blok: één rij per model op de server (plus een STANDAARD-
    // rij bovenaan), met een eigen notitie, kwaliteit en denktijd - zie de
    // toelichting in Setting-06.xaml. Vervangt de losse sterren/denktijd-
    // combo's die eerder onder elke van de vijf rolkeuzelijsten stonden:
    // die lieten bij een gedeeld model (bijvoorbeeld hetzelfde model voor
    // Daily chat EN Afbeeldingen) twee kopietjes van dezelfde instelling
    // zien die niet synchroon bleven.
    // ------------------------------------------------------------------

    private List<SterrenOptie> SterrenOpties()
    {
        var opties = new List<SterrenOptie> { new(Teksten.Van(this, "T_AiSterrenAutomatisch", "Quality: estimated"), null) };
        opties.AddRange(Enumerable.Range(0, 6).Select(n => new SterrenOptie(new string('★', n) + new string('☆', 5 - n), n)));
        return opties;
    }

    private List<DenktijdOptie> DenktijdOpties() => DenktijdWaarden
        .Select(seconden => new DenktijdOptie(
            seconden == 0
                ? Teksten.Van(this, "T_AiDenktijdStandaard", "Thinking time: default")
                : Teksten.Vul(this, "T_AiDenktijdFormaat", "Thinking time: max {0}s", seconden),
            seconden))
        .ToList();

    /// <summary>Bouwt het hele MODELLEN-blok opnieuw op: de STANDAARD-rij, dan één rij per model uit <paramref name="namen"/>.</summary>
    private void VulModellenBlok(List<string> namen)
    {
        ModellenBlok.Children.Clear();
        ModellenBlok.Children.Add(BouwModelRij(AiServerSettings.StandaardSleutel, standaard: true));

        foreach (var naam in namen)
        {
            ModellenBlok.Children.Add(BouwModelRij(naam, standaard: false));
        }
    }

    /// <summary>Eén rij: modelnaam (+ grootte/soort), een notitie, kwaliteit en denktijd.</summary>
    private UIElement BouwModelRij(string model, bool standaard)
    {
        var rij = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });

        for (var i = 0; i < 5; i++)
        {
            rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        }

        var tekstKleur = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
        var gedempt = TryFindResource("TextMutedBrush") as Brush ?? Brushes.Gray;
        var accent = TryFindResource("AccentBrush") as Brush ?? Brushes.Orange;
        Modelkeuze? keuze = null;

        if (standaard)
        {
            var label = new TextBlock
            {
                Text = Teksten.Van(this, "T_AiStandaardTitel", "Default for all models (a model's own value wins over this)"),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = accent,
                Margin = new Thickness(0, 0, 12, 0),
            };
            Grid.SetColumn(label, 0);
            Grid.SetColumnSpan(label, 2);
            rij.Children.Add(label);
        }
        else
        {
            keuze = Modelkeuze.Van(model, SoortNaam, _settings.Active.SterrenVoor(model));

            var naamBlok = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 12, 0) };
            naamBlok.Children.Add(new TextBlock { Text = keuze.Naam, Foreground = tekstKleur, TextTrimming = TextTrimming.CharacterEllipsis });
            naamBlok.Children.Add(new TextBlock
            {
                Text = keuze.Info,
                Foreground = gedempt,
                FontSize = TryFindResource("FontSizeSmall") is double klein ? klein : 12,
            });
            Grid.SetColumn(naamBlok, 0);
            rij.Children.Add(naamBlok);

            // Eigen notitie als die er is; anders alvast de automatische
            // inschatting op de modelnaam (zie Modelkeuze.AutomatischeOmschrijving)
            // - bewust bewerkbaar en niet als grijze plaatshoudertekst, zodat
            // je hem meteen kunt bijschaven. Onaangeraakt laten bewaart 'm
            // niet: pas typen en wegklikken zet 'm vast (zie WijzigOmschrijvingAsync).
            var eigenOmschrijving = _settings.Active.OmschrijvingVoor(model);
            var omschrijving = new TextBox
            {
                Text = eigenOmschrijving.Length > 0 ? eigenOmschrijving : Modelkeuze.AutomatischeOmschrijving(model),
                Style = TryFindResource("InputBoxStyle") as Style,
                // InputBoxStyle zet zelf Height="34" (voor de gewone, ééncijferige
                // velden) - dat wint altijd van MinHeight/MaxHeight, dus hier expliciet
                // terug naar automatisch zodat dit veld echt met de tekst meegroeit.
                Height = double.NaN,
                MinHeight = 34,
                MaxHeight = 160,
                Padding = new Thickness(10, 6, 10, 6),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 12, 0),
            };
            ToolTipService.SetToolTip(
                omschrijving,
                Teksten.Van(this, "T_AiOmschrijvingTip", "Note on what this model is good at - for yourself, not used by the apps. Grows with more lines if needed."));

            // Alleen ECHT bewaren als de tekst is veranderd sinds het veld
            // zijn waarde kreeg - anders zou je door er even in te klikken en
            // weer weg te klikken (zonder iets te wijzigen) de automatische
            // suggestie alsnog vastzetten als jouw eigen notitie.
            var basisTekst = omschrijving.Text;
            omschrijving.LostFocus += async (_, _) =>
            {
                if (omschrijving.Text.Trim() == basisTekst.Trim())
                {
                    return;
                }

                basisTekst = omschrijving.Text;
                await WijzigOmschrijvingAsync(model, omschrijving.Text);
            };
            Grid.SetColumn(omschrijving, 1);
            rij.Children.Add(omschrijving);
        }

        // Kwaliteit en denktijd onder elkaar in plaats van naast elkaar -
        // scheelt breedte, en de twee horen toch al bij elkaar.
        var beoordelingBlok = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 8, 0) };

        var sterrenOpties = SterrenOpties();
        var sterren = new ComboBox { ItemsSource = sterrenOpties, DisplayMemberPath = nameof(SterrenOptie.Label), Height = 34, Margin = new Thickness(0, 0, 0, 6) };
        sterren.SelectedItem = sterrenOpties.FirstOrDefault(optie => optie.Waarde == _settings.Active.SterrenVoor(model));
        sterren.SelectionChanged += async (_, _) =>
        {
            if (_laden || !IsLoaded)
            {
                return;
            }

            await WijzigSterrenAsync(model, (sterren.SelectedItem as SterrenOptie)?.Waarde);
        };
        beoordelingBlok.Children.Add(sterren);

        var denktijdOpties = DenktijdOpties();
        var denktijd = new ComboBox { ItemsSource = denktijdOpties, DisplayMemberPath = nameof(DenktijdOptie.Label), Height = 34 };
        denktijd.SelectedItem = denktijdOpties.FirstOrDefault(optie => optie.Seconden == _settings.Active.DenktijdVoor(model)) ?? denktijdOpties[0];
        denktijd.SelectionChanged += async (_, _) =>
        {
            if (_laden || !IsLoaded)
            {
                return;
            }

            await WijzigDenktijdAsync(model, (denktijd.SelectedItem as DenktijdOptie)?.Seconden ?? 0);
        };
        beoordelingBlok.Children.Add(denktijd);

        Grid.SetColumn(beoordelingBlok, 2);
        rij.Children.Add(beoordelingBlok);

        // De STANDAARD-rij gaat over sterren/denktijd, niet over taken - een
        // model is nooit "het" model voor een taak totdat je het kiest, dus
        // een geschiktheidstabel zou hier niets betekenen.
        if (!standaard && keuze is not null)
        {
            var taken = keuze.Taken;
            var scores = new[] { taken.Chat, taken.Code, taken.Controle, taken.Beeld, taken.Zoeken };

            for (var i = 0; i < scores.Length; i++)
            {
                var cel = new TextBlock
                {
                    Text = scores[i].ToString(System.Globalization.CultureInfo.CurrentCulture),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 8, 0, 0),
                    Foreground = scores[i] >= 5 ? accent : scores[i] == 0 ? gedempt : tekstKleur,
                    FontWeight = scores[i] >= 5 ? FontWeights.SemiBold : FontWeights.Normal,
                };
                Grid.SetColumn(cel, 3 + i);
                rij.Children.Add(cel);
            }
        }

        return rij;
    }

    private async Task WijzigSterrenAsync(string model, int? waarde)
    {
        var huidig = _settings.Active;
        var sterren = new Dictionary<string, int>(huidig.Sterren ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);

        if (waarde is { } gezet)
        {
            sterren[model] = gezet;
        }
        else
        {
            sterren.Remove(model);
        }

        await ToepassenAsync(_settings.With(HuidigeProvider(), huidig with { Sterren = sterren }));
        VerversModelkeuzelijsten();
    }

    private async Task WijzigDenktijdAsync(string model, int seconden)
    {
        var huidig = _settings.Active;
        var denktijden = new Dictionary<string, int>(huidig.MaxDenktijdSeconden ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);

        if (seconden > 0)
        {
            denktijden[model] = seconden;
        }
        else
        {
            denktijden.Remove(model);
        }

        await ToepassenAsync(_settings.With(HuidigeProvider(), huidig with { MaxDenktijdSeconden = denktijden }));
    }

    private async Task WijzigOmschrijvingAsync(string model, string tekst)
    {
        var huidig = _settings.Active;

        if (string.Equals(huidig.OmschrijvingVoor(model), tekst.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        var omschrijvingen = new Dictionary<string, string>(huidig.Omschrijvingen ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(tekst))
        {
            omschrijvingen.Remove(model);
        }
        else
        {
            omschrijvingen[model] = tekst.Trim();
        }

        await ToepassenAsync(_settings.With(HuidigeProvider(), huidig with { Omschrijvingen = omschrijvingen }));
    }

    private AiProvider HuidigeProvider() => ProviderOllama.IsChecked == true ? AiProvider.Ollama : AiProvider.LmStudio;

    /// <summary>
    /// De sterren bij een modelnaam in de vijf rolkeuzelijsten (zie
    /// ModelkeuzeTemplate) opnieuw laten kloppen na een wijziging in het
    /// MODELLEN-blok - zonder dat blok zelf opnieuw op te bouwen, want dat
    /// zou elke openstaande notitie zijn focus/cursor laten verliezen.
    /// </summary>
    private void VerversModelkeuzelijsten()
    {
        _laden = true;
        try
        {
            HerbouwKeuzelijst(ChatModelPicker, _settings.Active.ChatModel);
            HerbouwKeuzelijst(EmbedModelPicker, _settings.Active.EmbedModel);
            HerbouwKeuzelijst(CodeModelPicker, _settings.Active.CodeModel);
            HerbouwKeuzelijst(ControleModelPicker, _settings.Active.ControleModel);
            HerbouwKeuzelijst(BeeldModelPicker, _settings.Active.BeeldModel);
        }
        finally
        {
            _laden = false;
        }
    }

    /// <summary>Stuurt de huidige stand van de kaart naar de broker.</summary>
    private async Task BewaarAsync()
    {
        var provider = HuidigeProvider();

        // Wat er op de kaart staat hoort bij de soort die NU gekozen is; de
        // andere soort blijft staan zoals hij stond, zodat heen en weer
        // wisselen niets wist - niet het adres en niet de modellen. Sterren,
        // denktijd en omschrijvingen staan al in _settings.Active en blijven
        // dus ook automatisch staan - die komen hier niet aan bod.
        var huidig = _settings.Active;
        var server = huidig with
        {
            Url = ServerUrlBox.Text,
            ChatModel = Gekozen(ChatModelPicker),
            EmbedModel = Gekozen(EmbedModelPicker),
            CodeModel = Gekozen(CodeModelPicker),
            ControleModel = Gekozen(ControleModelPicker),
            BeeldModel = Gekozen(BeeldModelPicker),
        };

        await ToepassenAsync((_settings with { Provider = provider }).With(provider, server));
    }

    /// <summary>
    /// Stuurt een nieuwe instelling naar de broker en houdt bij wat daar nu
    /// staat. Alles wat opslaat komt hierlangs, zodat het melden en het
    /// opnieuw laten controleren op een plek staat.
    /// </summary>
    private async Task ToepassenAsync(AiSettings nieuw)
    {
        var result = await _broker.SaveSettingsAsync(nieuw, CancellationToken.None);
        if (!result.Success)
        {
            Meld(Teksten.Vul(this, "T_AiNietBewaardFormaat", "Not saved: {0}", result.ErrorMessage));
            return;
        }

        _settings = result.Settings;
        ActivityLog.Shared.Add(
            "app",
            $"AI ingesteld: {_settings.Provider}, chat '{_settings.Active.ChatModel}', embedding '{_settings.Active.EmbedModel}', " +
            $"code '{_settings.Active.CodeModel}', controle '{_settings.Active.ControleModel}'");

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private void Meld(string tekst) => StatusText.Text = tekst;

    /// <summary>De meldingregel staat in de oude taal tot hij opnieuw opgebouwd wordt - dus dat doen we.</summary>
    private async void OnTaalGewisseld(object? sender, EventArgs e) => await VulModellenAsync();

    /// <summary>
    /// Andere serversoort gekozen. Alleen de SOORT gaat om: het adres en de
    /// modellen van die soort staan al bewaard en komen hier weer
    /// tevoorschijn.
    ///
    /// Met opzet niet via BewaarAsync: daar wordt opgeslagen wat er op de
    /// kaart staat, en dat zijn op dit moment nog de modellen van de soort
    /// waar je net vandaan komt. Die zouden dan onder de nieuwe soort
    /// terechtkomen.
    /// </summary>
    private async void Provider_Checked(object sender, RoutedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        var provider = ReferenceEquals(sender, ProviderOllama) ? AiProvider.Ollama : AiProvider.LmStudio;

        await ToepassenAsync(_settings with { Provider = provider });

        _laden = true;
        try
        {
            ServerUrlBox.Text = _settings.ActiveServerUrl;
        }
        finally
        {
            _laden = false;
        }

        // Vult de keuzelijsten met wat er op DEZE server staat en kiest
        // daarin de modellen die voor deze soort bewaard zijn.
        await VulModellenAsync();
    }

    private async void ServerUrl_LostFocus(object sender, RoutedEventArgs e) => await AdresVerwerkenAsync();

    /// <summary>Enter doet hetzelfde als wegklikken - anders moet je raden wanneer het adres aankomt.</summary>
    private async void ServerUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await AdresVerwerkenAsync();
    }

    private async Task AdresVerwerkenAsync()
    {
        if (_laden || !IsLoaded || string.Equals(ServerUrlBox.Text.Trim(), _settings.ActiveServerUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await BewaarAsync();
        await VulModellenAsync();
    }

    private async void Model_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        await BewaarAsync();
    }

    private async void RefreshModels_Click(object sender, RoutedEventArgs e) => await VulModellenAsync();

    /// <summary>
    /// Klapt het MODELLEN-blok open of dicht. Staat standaard dicht (zie
    /// Setting-06.xaml, ModellenSectie.Visibility="Collapsed") - een lijst
    /// met soms tientallen modellen en hun notities hoeft niet altijd in
    /// beeld te staan.
    /// </summary>
    private void ModellenToggle_Click(object sender, RoutedEventArgs e) =>
        ModellenSectie.Visibility = ModellenToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Ververst alleen het opschrift, terwijl je sleept. Niet opslaan: dit
    /// vuurt tientallen keren per seconde tijdens het slepen, en een
    /// schuifbalk naar de broker schrijven bij elke tik zou de instelling
    /// (en het activiteitenlog) onnodig laten spammen - zie
    /// Ondergrens_Vastgezet, dat pas bewaart als je loslaat.
    /// </summary>
    private void Ondergrens_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ToonOndergrens(e.NewValue);

    /// <summary>
    /// Het slepen is klaar (muis losgelaten) of een toetsaanslag is
    /// verwerkt (pijltjestoetsen) - nu pas opslaan. Dezelfde afweging als
    /// bij een splitter (Layout/SplitterLayout): bewaren aan het EIND van
    /// een beweging, niet tijdens.
    /// </summary>
    private async void Ondergrens_Vastgezet(object sender, RoutedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        var nieuw = OndergrensSlider.Value / 100.0;

        if (Math.Abs(nieuw - _settings.MinimumSemanticScore) < 0.001)
        {
            return;
        }

        await ToepassenAsync(_settings with { MinimumSemanticScore = nieuw });
    }

    /// <summary>Ververst alleen het opschrift, terwijl je sleept - zelfde reden als Ondergrens_ValueChanged.</summary>
    private void ContextRondes_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ToonContextRondes(e.NewValue);

    /// <summary>Pas bewaren als je loslaat - zelfde afweging als Ondergrens_Vastgezet.</summary>
    private async void ContextRondes_Vastgezet(object sender, RoutedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        var nieuw = (int)Math.Round(ContextRondesSlider.Value);

        if (nieuw == _settings.MaxContextRondes)
        {
            return;
        }

        await ToepassenAsync(_settings with { MaxContextRondes = nieuw });
    }

    /// <summary>Ververst alleen het opschrift, terwijl je sleept - zelfde reden als Ondergrens_ValueChanged.</summary>
    private void ChatTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ToonChatTimeout(e.NewValue);

    /// <summary>Pas bewaren als je loslaat - zelfde afweging als Ondergrens_Vastgezet.</summary>
    private async void ChatTimeout_Vastgezet(object sender, RoutedEventArgs e)
    {
        if (_laden || !IsLoaded)
        {
            return;
        }

        var nieuw = Math.Round(ChatTimeoutSlider.Value) * 60;

        if (Math.Abs(nieuw - _settings.ChatTimeoutSeconden) < 0.001)
        {
            return;
        }

        await ToepassenAsync(_settings with { ChatTimeoutSeconden = nieuw });
    }
}
