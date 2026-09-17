using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using Jabasoft.Base.Health;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Healthkaart.xaml - see that file for what it looks like.</summary>
public partial class Healthkaart : UserControl
{
    public Healthkaart()
    {
        InitializeComponent();
    }

    /// <summary>Zet de kaart op de uitslag van één controle.</summary>
    public void Show(HealthCheckResult uitslag)
    {
        ArgumentNullException.ThrowIfNull(uitslag);

        NaamText.Text = uitslag.Name;
        MeldingText.Text = uitslag.Message;

        // Dezelfde drie kleuren als de SYSTEM HEALTH-pil in de footer, uit
        // het thema: groen is goed, oranje is bezig, rood is fout.
        var sleutel = uitslag.State switch
        {
            HealthState.Healthy => "HealthOkBrush",
            HealthState.Failed => "HealthErrorBrush",
            _ => "HealthBusyBrush",
        };

        if (TryFindResource(sleutel) is Brush kleur)
        {
            Bolletje.Fill = kleur;
        }

        // Duur alleen tonen als er echt gemeten is; een controle die nog
        // moet beginnen staat op nul en dat is geen informatie.
        DuurText.Text = uitslag.Duration > TimeSpan.Zero
            ? string.Format(CultureInfo.CurrentCulture, "{0:N0} ms", uitslag.Duration.TotalMilliseconds)
            : string.Empty;
    }
}
