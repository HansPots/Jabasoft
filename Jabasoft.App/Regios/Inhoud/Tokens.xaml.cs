using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Controls;
using Jabasoft.App.Taal;
using Jabasoft.Base.AiBroker;

namespace Jabasoft.App.Regios.Inhoud;

/// <summary>
/// Interaction logic for Tokens.xaml - see that file for what it looks like.
///
/// Haalt het overzicht bij de broker op: die leest de gedeelde tabel
/// TokenUsageEntries uit, deze app praat zelf niet met de database.
///
/// In twee stappen: eerst de weektotalen (één vraag), en de losse regels van
/// een week pas wanneer je die week openklapt. Een jaar aan weken opent dus
/// geen jaar aan regels.
/// </summary>
public partial class Tokens : UserControl
{
    private readonly IAiBrokerClient _broker = AiBrokerClient.CreateDefault();

    public Tokens()
    {
        InitializeComponent();

        Loaded += async (_, _) => await LaadAsync();
    }

    /// <summary>Haalt de weektotalen op en zet er een kaart per week neer. Loopt elke keer dat het scherm in beeld komt, zodat nieuw verbruik meteen meetelt.</summary>
    public async Task LaadAsync()
    {
        var weken = await _broker.GetUsageWeeksAsync(CancellationToken.None);

        Totaal.Show(weken);
        await LaadVerloopAsync(weken);
        Weken.Items.Clear();

        if (weken.Count == 0)
        {
            Melden(Teksten.Van(this, "T_TokensLeeg", "Nog geen AI-aanroepen vastgelegd."));
            return;
        }

        Melden(null);

        foreach (var week in weken)
        {
            var kaart = new Tokenweek
            {
                // Breedte los: de kaart brengt zijn eigen 1568 mee (om op
                // zichzelf te bekijken), hier moet hij meerekken met het vak.
                Width = double.NaN,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Als DynamicResource en niet als vaste waarde: dan loopt de
            // ruimte tussen de kaarten mee met de SPACING-instelling, net
            // als op het instellingenscherm.
            kaart.SetResourceReference(MarginProperty, "RegionGapTopThickness");

            kaart.Show(week);
            kaart.Openklappen += async (afzender, sleutel) => await OpenklappenAsync((Tokenweek)afzender!, sleutel);

            Weken.Items.Add(kaart);
        }
    }

    /// <summary>Hoeveel dagen de grafiek terugkijkt: vier volle weken.</summary>
    private const int VerloopDagen = 28;

    /// <summary>
    /// Het verloop per model voor de grafiek: de losse aanroepen van de weken
    /// die in de laatste <see cref="VerloopDagen"/> vallen, opgeteld per model
    /// en per dag. Eén vraag per week, dus hooguit vijf - de weekkaarten
    /// blijven zelf hun regels pas ophalen bij het openklappen.
    /// </summary>
    private async Task LaadVerloopAsync(IReadOnlyList<TokenUsageWeek> weken)
    {
        var tot = DateTime.Today;
        var van = tot.AddDays(1 - VerloopDagen);

        var sleutels = weken.Where(week => week.End.Date >= van).Select(week => week.Week).ToList();
        var lijsten = await Task.WhenAll(sleutels.Select(sleutel => _broker.GetUsageEntriesAsync(sleutel, CancellationToken.None)));

        var onbekend = Teksten.Van(this, "T_TokensOnbekendModel", "(onbekend)");
        var perModel = new Dictionary<string, Dictionary<DateTime, long>>();

        foreach (var regel in lijsten.SelectMany(lijst => lijst))
        {
            var dag = regel.Timestamp.LocalDateTime.Date;

            if (dag < van || dag > tot)
            {
                continue;
            }

            var model = string.IsNullOrWhiteSpace(regel.Model) ? onbekend : regel.Model;

            if (!perModel.TryGetValue(model, out var perDag))
            {
                perModel[model] = perDag = [];
            }

            perDag[dag] = perDag.GetValueOrDefault(dag) + regel.TotalTokens;
        }

        Grafiek.Show(
            perModel.ToDictionary(model => model.Key, model => (IReadOnlyDictionary<DateTime, long>)model.Value),
            van,
            tot);
    }

    private async Task OpenklappenAsync(Tokenweek kaart, string week)
    {
        var regels = await _broker.GetUsageEntriesAsync(week, CancellationToken.None);
        kaart.ToonRegels(regels);
    }

    private void Melden(string? tekst)
    {
        MeldingText.Text = tekst ?? string.Empty;
        MeldingText.Visibility = string.IsNullOrEmpty(tekst) ? Visibility.Collapsed : Visibility.Visible;
    }
}
