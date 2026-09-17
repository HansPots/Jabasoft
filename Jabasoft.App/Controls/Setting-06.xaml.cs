using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        }
        finally
        {
            _laden = false;
        }

        if (!result.Success)
        {
            Meld(Teksten.Vul(this, "T_AiBrokerWegFormaat", "Broker not reachable: {0}", result.ErrorMessage));
            return;
        }

        await VulModellenAsync();
    }

    /// <summary>
    /// Vraagt de broker welke modellen er op de ingestelde server AANWEZIG
    /// zijn en zet die in de twee keuzelijsten.
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
    private static void Vul(ComboBox keuzelijst, List<string> namen, string ingesteld)
    {
        var items = new List<string>(namen);

        if (!string.IsNullOrWhiteSpace(ingesteld) && !items.Contains(ingesteld, StringComparer.OrdinalIgnoreCase))
        {
            items.Insert(0, ingesteld);
        }

        keuzelijst.ItemsSource = items;
        keuzelijst.SelectedItem = items.FirstOrDefault(item => string.Equals(item, ingesteld, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Stuurt de huidige stand van de kaart naar de broker.</summary>
    private async Task BewaarAsync()
    {
        var provider = ProviderOllama.IsChecked == true ? AiProvider.Ollama : AiProvider.LmStudio;

        // Wat er op de kaart staat hoort bij de soort die NU gekozen is; de
        // andere soort blijft staan zoals hij stond, zodat heen en weer
        // wisselen niets wist - niet het adres en niet de modellen.
        var server = new AiServerSettings(
            ServerUrlBox.Text,
            ChatModelPicker.SelectedItem as string ?? string.Empty,
            EmbedModelPicker.SelectedItem as string ?? string.Empty);

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
            $"AI ingesteld: {_settings.Provider}, chat '{_settings.Active.ChatModel}', embedding '{_settings.Active.EmbedModel}'");

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
}
