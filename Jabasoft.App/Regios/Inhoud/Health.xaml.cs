using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jabasoft.App.Controls;
using Jabasoft.Base.Health;

namespace Jabasoft.App.Regios.Inhoud;

/// <summary>
/// Interaction logic for Health.xaml - see that file for what it looks like.
///
/// Kijkt mee met dezelfde <see cref="HealthMonitor"/> als de pil in de
/// footer - er is er één per applicatie. Dit scherm bewaakt dus niets
/// zelf; het laat alleen zien wat die monitor gevonden heeft, en kan hem
/// opnieuw laten lopen.
/// </summary>
public partial class Health : UserControl
{
    private HealthMonitor? _monitor;

    /// <summary>Eén kaart per controle, in dezelfde volgorde als de monitor ze aflegt.</summary>
    private readonly List<Healthkaart> _kaarten = [];

    public Health()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Koppelt het scherm aan de monitor van de app. Een tweede aanroep
    /// vervangt de vorige - zelfde afspraak als Statusbalk.Observe, zodat
    /// er nooit twee koppelingen tegelijk staan.
    /// </summary>
    public void Observe(HealthMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        if (_monitor is not null)
        {
            _monitor.Changed -= OnChanged;
            _monitor.ResultsChanged -= OnResultsChanged;
        }

        _monitor = monitor;
        _monitor.Changed += OnChanged;
        _monitor.ResultsChanged += OnResultsChanged;

        // Meteen de stand van nu tonen: de monitor kan al gelopen hebben
        // voordat dit scherm in beeld kwam.
        ToonStand(monitor.Current);
        ToonUitslagen(monitor.Results);
    }

    private void OnChanged(object? sender, HealthStatus status) => ToonStand(status);

    private void OnResultsChanged(object? sender, IReadOnlyList<HealthCheckResult> uitslagen) => ToonUitslagen(uitslagen);

    private void ToonStand(HealthStatus status)
    {
        StandText.Text = status.Message;

        var sleutel = status.State switch
        {
            HealthState.Healthy => "HealthOkBrush",
            HealthState.Failed => "HealthErrorBrush",
            _ => "HealthBusyBrush",
        };

        if (TryFindResource(sleutel) is Brush kleur)
        {
            Bolletje.Fill = kleur;
        }
    }

    /// <summary>
    /// Zet de kaarten bij op het aantal controles en vult ze. Bijzetten en
    /// niet opnieuw opbouwen: dit komt bij elke stap van de controle langs,
    /// en dan zou het scherm bij elke tussenstand opnieuw flikkeren.
    /// </summary>
    private void ToonUitslagen(IReadOnlyList<HealthCheckResult> uitslagen)
    {
        while (_kaarten.Count < uitslagen.Count)
        {
            var kaart = new Healthkaart
            {
                // De kaart brengt zijn eigen breedte mee om op zichzelf te
                // bekijken; hier moet hij meerekken met het inhoudsvak.
                Width = double.NaN,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Als DynamicResource, zodat de ruimte tussen de kaarten
            // meeloopt met de SPACING-instelling.
            kaart.SetResourceReference(MarginProperty, "RegionGapTopThickness");

            _kaarten.Add(kaart);
            Controles.Items.Add(kaart);
        }

        for (var i = 0; i < uitslagen.Count; i++)
        {
            _kaarten[i].Show(uitslagen[i]);
        }
    }

    private void Opnieuw_Click(object sender, RoutedEventArgs e)
    {
        // Niet awaiten: het scherm vult zichzelf bij terwijl de controle
        // loopt. Loopt er al een ronde, dan doet dit niets - dat bewaakt de
        // monitor zelf.
        _ = _monitor?.RunAsync();
    }
}
