using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Controls;
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
        Weken.Items.Clear();

        if (weken.Count == 0)
        {
            Melden("Nog geen AI-aanroepen vastgelegd. Zodra een applicatie de broker gebruikt, verschijnt hier een week.");
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
