using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Taal;
using Jabasoft.Base.AiBroker;

namespace Jabasoft.App.Controls;

/// <summary>Eén regel in het opengeklapte weekoverzicht, al klaar om te tonen.</summary>
public sealed record TokenRegel(string Tijd, string App, string Model, string Prompt, string Antwoord, string Totaal);

/// <summary>
/// Interaction logic for Tokenweek.xaml - see that file for what it looks like.
///
/// Het component haalt zelf geen regels op: het meldt alleen dát het
/// opengeklapt is (<see cref="Openklappen"/>) en wacht tot iemand
/// <see cref="ToonRegels"/> aanroept. Zo hoeft dit component niets van de
/// broker te weten, net als de andere controls van deze app.
/// </summary>
public partial class Tokenweek : UserControl
{
    private bool _opgehaald;

    public Tokenweek()
    {
        InitializeComponent();
    }

    /// <summary>De sleutel van deze week, bijvoorbeeld "2026-W38".</summary>
    public string Week { get; private set; } = string.Empty;

    /// <summary>
    /// Gaat af als de week voor het EERST opengeklapt wordt. Daarna niet
    /// meer: de regels van een week die al voorbij is veranderen niet, en
    /// opnieuw ophalen bij elk klikje is zonde van de databasevraag.
    /// </summary>
    public event EventHandler<string>? Openklappen;

    /// <summary>Zet de kopregel: welke week, welke periode, en het totaal.</summary>
    public void Show(TokenUsageWeek week)
    {
        ArgumentNullException.ThrowIfNull(week);

        Week = week.Week;

        // "week 38" leest prettiger dan de sleutel zelf; het jaartal staat
        // toch al in de periode eronder.
        var nummer = week.Week.Split('W') is { Length: 2 } delen ? delen[1] : week.Week;
        WeekText.Text = Teksten.Vul(this, "T_TokensWeekFormaat", "week {0}", nummer.TrimStart('0'));

        PeriodeText.Text = Teksten.Vul(
            this,
            "T_TokensPeriodeFormaat",
            "{0:d MMMM} t/m {1:d MMMM yyyy} · {2:N0} {3}",
            week.Start,
            week.End,
            week.Calls,
            Teksten.Van(this, week.Calls == 1 ? "T_TokensAanroep" : "T_TokensAanroepen", week.Calls == 1 ? "aanroep" : "aanroepen"));

        TotaalText.Text = week.TotalTokens.ToString("N0", CultureInfo.CurrentCulture);
    }

    /// <summary>Vult het opengeklapte deel met de aanroepen van deze week.</summary>
    public void ToonRegels(IReadOnlyList<TokenUsageEntry> regels)
    {
        ArgumentNullException.ThrowIfNull(regels);

        Regels.ItemsSource = regels
            .Select(regel => new TokenRegel(
                regel.Timestamp.LocalDateTime.ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture),
                regel.Application,
                regel.Model ?? "-",
                regel.PromptTokens.ToString("N0", CultureInfo.CurrentCulture),
                regel.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture),
                regel.TotalTokens.ToString("N0", CultureInfo.CurrentCulture)))
            .ToList();

        Melden(regels.Count == 0 ? Teksten.Van(this, "T_TokensGeenRegels", "Geen regels gevonden voor deze week.") : null);
    }

    /// <summary>Een mededeling in plaats van regels - bijvoorbeeld terwijl er opgehaald wordt.</summary>
    public void Melden(string? tekst)
    {
        MeldingText.Text = tekst ?? string.Empty;
        MeldingText.Visibility = string.IsNullOrEmpty(tekst) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Reageert op de TOESTAND van de knop en niet op de klik: zo werkt het
    /// ook met het toetsenbord of via schermbediening, die de knop omzetten
    /// zonder een muisklik te sturen.
    /// </summary>
    private void Kop_Omgezet(object sender, RoutedEventArgs e)
    {
        var open = Kop.IsChecked == true;
        Regelvak.Visibility = open ? Visibility.Visible : Visibility.Collapsed;

        if (!open || _opgehaald)
        {
            return;
        }

        _opgehaald = true;
        Melden(Teksten.Van(this, "T_TokensOphalen", "Ophalen…"));
        Openklappen?.Invoke(this, Week);
    }
}
