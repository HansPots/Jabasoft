using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Jabasoft.App.Taal;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Tokengrafiek.xaml - see that file for what it looks like.
///
/// Tekent een lijn per model met de tokens per dag. Dagen zonder verbruik
/// tellen als nul mee, anders zou een lijn een stille dag overbruggen alsof
/// er iets gebeurd is.
/// </summary>
public partial class Tokengrafiek : UserControl
{
    /// <summary>
    /// Kleuren per lijn, herkenbaar op de donkere achtergrond. De eerste is de
    /// accentkleur van het thema (zie Kleur), de rest een vaste reeks - een
    /// model houdt zolang de volgorde gelijk blijft dezelfde kleur.
    /// </summary>
    private static readonly Color[] Reeks =
    [
        Color.FromRgb(0x6F, 0xB8, 0xE8),
        Color.FromRgb(0xB7, 0x8B, 0xE6),
        Color.FromRgb(0x7F, 0xD0, 0x8A),
        Color.FromRgb(0xE8, 0xD0, 0x6F),
        Color.FromRgb(0xE8, 0x7F, 0x8A),
        Color.FromRgb(0x7F, 0xDA, 0xD0),
        Color.FromRgb(0xC9, 0xC9, 0xC9),
    ];

    private const double MargeLinks = 64;
    private const double MargeRechts = 16;
    private const double MargeBoven = 10;
    private const double MargeOnder = 26;

    private IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, long>> _data =
        new Dictionary<string, IReadOnlyDictionary<DateTime, long>>();

    private DateTime _van;
    private DateTime _tot;

    public Tokengrafiek()
    {
        InitializeComponent();

        Vlak.SizeChanged += (_, _) => Teken();
    }

    /// <summary>
    /// Zet de gegevens neer: per model de tokens per dag, over de dagen
    /// <paramref name="van"/> tot en met <paramref name="tot"/>.
    /// </summary>
    public void Show(IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, long>> perModel, DateTime van, DateTime tot)
    {
        _data = perModel;
        _van = van.Date;
        _tot = tot.Date;

        var totaal = perModel.Values.Sum(dagen => dagen.Values.Sum());

        Onderschrift.Text = totaal == 0
            ? Teksten.Van(this, "T_TokensVerloopLeeg", "Nog geen verloop om te tonen.")
            : string.Format(
                CultureInfo.CurrentCulture,
                Teksten.Van(this, "T_TokensVerloopOnderschrift", "Tokens per dag, {0} tot {1}"),
                _van.ToString("d MMM", CultureInfo.CurrentCulture),
                _tot.ToString("d MMM", CultureInfo.CurrentCulture));

        Teken();
    }

    private Brush Kleur(int nummer) =>
        nummer == 0 && TryFindResource("AccentBrush") is Brush accent
            ? accent
            : new SolidColorBrush(Reeks[nummer % Reeks.Length]);

    private Brush Thema(string sleutel, Brush terugval) =>
        TryFindResource(sleutel) as Brush ?? terugval;

    private void Teken()
    {
        Vlak.Children.Clear();
        Legenda.Children.Clear();

        var breedte = Vlak.ActualWidth;
        var hoogte = Vlak.ActualHeight;

        if (_data.Count == 0 || breedte < 200 || hoogte < 80)
        {
            return;
        }

        var dagen = (_tot - _van).Days + 1;

        if (dagen < 2)
        {
            return;
        }

        var lijnKleur = Thema("BorderBrush", Brushes.Gray);
        var tekstKleur = Thema("TextMutedBrush", Brushes.LightGray);

        var vlakBreedte = breedte - MargeLinks - MargeRechts;
        var vlakHoogte = hoogte - MargeBoven - MargeOnder;

        var hoogste = _data.Values.SelectMany(dag => dag.Values).DefaultIfEmpty(0).Max();
        var top = MooiMaximum(hoogste);

        double X(int dag) => MargeLinks + (dag * vlakBreedte / (dagen - 1));
        double Y(long waarde) => MargeBoven + vlakHoogte - (waarde * vlakHoogte / top);

        // Horizontale hulplijnen met de waarde erbij.
        for (var stap = 0; stap <= 4; stap++)
        {
            var waarde = top * stap / 4;
            var y = Y(waarde);

            Vlak.Children.Add(new Line
            {
                X1 = MargeLinks, X2 = MargeLinks + vlakBreedte, Y1 = y, Y2 = y,
                Stroke = lijnKleur, StrokeThickness = stap == 0 ? 2 : 1, Opacity = stap == 0 ? 1 : 0.5,
            });

            var label = new TextBlock
            {
                Text = Afkorten(waarde),
                Foreground = tekstKleur,
                FontSize = 12,
                Width = MargeLinks - 10,
                TextAlignment = TextAlignment.Right,
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 9);
            Vlak.Children.Add(label);
        }

        // Datums onderaan: eens per week, plus de laatste dag.
        for (var dag = 0; dag < dagen; dag += 7)
        {
            ZetDatum(_van.AddDays(dag), X(dag), hoogte, tekstKleur);
        }

        if ((dagen - 1) % 7 != 0)
        {
            ZetDatum(_tot, X(dagen - 1), hoogte, tekstKleur);
        }

        // Eén lijn per model, de zwaarste eerst - die krijgt zo de eerste kleur.
        var modellen = _data
            .OrderByDescending(model => model.Value.Values.Sum())
            .ToList();

        // Eerst alle lijnen, dan alle stippen: zo liggen de stippen (met hun
        // tooltip) altijd boven de lijnen, en blijft aanwijzen werken.
        var stippen = new List<UIElement>();

        for (var nummer = 0; nummer < modellen.Count; nummer++)
        {
            var (naam, perDag) = (modellen[nummer].Key, modellen[nummer].Value);
            var kleur = Kleur(nummer);
            var lijn = new Polyline
            {
                Stroke = kleur, StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round,
            };

            for (var dag = 0; dag < dagen; dag++)
            {
                var datum = _van.AddDays(dag);
                var waarde = perDag.TryGetValue(datum, out var gevonden) ? gevonden : 0;
                lijn.Points.Add(new Point(X(dag), Y(waarde)));

                if (waarde > 0)
                {
                    var stip = new Ellipse
                    {
                        Width = 8, Height = 8, Fill = kleur,
                        ToolTip = $"{naam}\n{datum:d MMM}: {waarde:N0} tokens",
                    };
                    Canvas.SetLeft(stip, X(dag) - 4);
                    Canvas.SetTop(stip, Y(waarde) - 4);
                    stippen.Add(stip);
                }
            }

            Vlak.Children.Add(lijn);
            Legenda.Children.Add(LegendaItem(naam, perDag.Values.Sum(), kleur));
        }

        foreach (var stip in stippen)
        {
            Vlak.Children.Add(stip);
        }
    }

    private void ZetDatum(DateTime datum, double x, double hoogte, Brush kleur)
    {
        var label = new TextBlock
        {
            Text = datum.ToString("d MMM", CultureInfo.CurrentCulture),
            Foreground = kleur,
            FontSize = 12,
        };
        Canvas.SetLeft(label, x - 18);
        Canvas.SetTop(label, hoogte - MargeOnder + 6);
        Vlak.Children.Add(label);
    }

    private UIElement LegendaItem(string model, long totaal, Brush kleur)
    {
        var rij = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 24, 4) };
        rij.Children.Add(new Rectangle { Width = 14, Height = 4, Fill = kleur, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        rij.Children.Add(new TextBlock
        {
            Text = $"{model}  {totaal:N0}",
            FontSize = 12,
            Foreground = Thema("TextPrimaryBrush", Brushes.White),
        });
        return rij;
    }

    /// <summary>
    /// De bovenkant van de as: net boven de hoogste waarde, zodat er geen
    /// hoogte verloren gaat aan lege ruimte. De as heeft vier vakken, dus
    /// gezocht wordt de kleinste "ronde" vakhoogte (1, 1,2, 1,5, 2, 2,5, 3, 4,
    /// 5, 6, 8 of 10 keer een macht van tien) waarvan vier vakken de hoogste
    /// waarde nog omvatten - de hulplijnen komen zo op ronde getallen uit.
    /// </summary>
    private static long MooiMaximum(long hoogste)
    {
        if (hoogste <= 0)
        {
            return 4;
        }

        var perVak = hoogste / 4.0;
        var macht = Math.Pow(10, Math.Floor(Math.Log10(perVak)));

        foreach (var factor in new[] { 1.0, 1.2, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 8.0, 10.0 })
        {
            var vak = factor * macht;

            if (vak * 4 >= hoogste)
            {
                return (long)Math.Max(4, Math.Ceiling(vak * 4));
            }
        }

        return (long)Math.Ceiling(40 * macht);
    }

    private static string Afkorten(long waarde) => waarde switch
    {
        >= 1_000_000 => (waarde / 1_000_000.0).ToString("0.#", CultureInfo.CurrentCulture) + " M",
        >= 1_000 => (waarde / 1_000.0).ToString("0.#", CultureInfo.CurrentCulture) + " K",
        _ => waarde.ToString(CultureInfo.CurrentCulture),
    };
}
