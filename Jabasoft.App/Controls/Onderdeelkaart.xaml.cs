using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jabasoft.App.Taal;
using Jabasoft.Base.AiBroker;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Onderdeelkaart.xaml - see that file for what it looks like.
///
/// Toont per onderdeel van een applicatie hoeveel tokens er naartoe gingen:
/// een staaf ten opzichte van het zwaarste onderdeel, met daarachter de
/// tokens en het aantal aanroepen. Regels van voor er onderdelen
/// vastgelegd werden staan onder "—".
/// </summary>
public partial class Onderdeelkaart : UserControl
{
    /// <summary>Hoeveel onderdelen er hooguit getoond worden - daarna wordt het een lange lijst zonder meerwaarde.</summary>
    private const int MaxStaven = 12;

    public Onderdeelkaart()
    {
        InitializeComponent();
    }

    /// <summary>Zet de gegevens neer: het verbruik per onderdeel over de laatste <paramref name="dagen"/> dagen.</summary>
    public void Show(IReadOnlyList<TokenUsageOnderdeel> onderdelen, int dagen)
    {
        Staven.Children.Clear();

        var totaal = onderdelen.Sum(o => o.TotalTokens);

        if (onderdelen.Count == 0 || totaal == 0)
        {
            Onderschrift.Text = Teksten.Van(this, "T_TokensOnderdeelLeeg", "Nog geen verbruik per onderdeel om te tonen.");
            return;
        }

        Onderschrift.Text = Teksten.Vul(
            this,
            "T_TokensOnderdeelOnderschrift",
            "Tokens per onderdeel, laatste {0} dagen - {1} in totaal",
            dagen,
            totaal.ToString("N0", CultureInfo.CurrentCulture));

        var hoogste = onderdelen.Max(o => o.TotalTokens);
        var accent = TryFindResource("AccentBrush") as Brush ?? Brushes.Orange;
        var gedempt = TryFindResource("TextMutedBrush") as Brush ?? Brushes.Gray;
        var tekst = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White;

        foreach (var onderdeel in onderdelen.Take(MaxStaven))
        {
            var deel = Math.Max(0.02, (double)onderdeel.TotalTokens / hoogste);
            var percentage = 100.0 * onderdeel.TotalTokens / totaal;

            var rij = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            rij.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rij.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var naam = new TextBlock
            {
                Text = $"{onderdeel.Application} · {onderdeel.Onderdeel}",
                Foreground = tekst,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 12, 0),
                ToolTip = $"{onderdeel.Application} · {onderdeel.Onderdeel}",
            };
            Grid.SetColumn(naam, 0);

            // De staaf: een vlak met een randje als baan, en daarin een
            // gevuld deel ter grootte van het aandeel ten opzichte van het zwaarste.
            var baan = new Grid { Height = 18, VerticalAlignment = VerticalAlignment.Center };
            baan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(deel, GridUnitType.Star) });
            baan.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - deel, GridUnitType.Star) });
            baan.Children.Add(new Border { Background = accent, CornerRadius = new CornerRadius(3), Opacity = 0.9 });
            Grid.SetColumn(baan, 1);

            var waarde = new TextBlock
            {
                Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Teksten.Van(this, "T_TokensOnderdeelWaardeFormaat", "{0} tokens · {1} aanroepen · {2:0}%"),
                    onderdeel.TotalTokens.ToString("N0", CultureInfo.CurrentCulture),
                    onderdeel.Calls.ToString("N0", CultureInfo.CurrentCulture),
                    percentage),
                Foreground = gedempt,
                FontSize = TryFindResource("FontSizeSmall") is double klein ? klein : 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            Grid.SetColumn(waarde, 2);

            rij.Children.Add(naam);
            rij.Children.Add(baan);
            rij.Children.Add(waarde);
            Staven.Children.Add(rij);
        }
    }
}
