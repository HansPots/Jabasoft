using System.Globalization;
using System.Windows.Controls;
using Jabasoft.Base.AiBroker;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Tokentotaal.xaml - see that file for what it looks like.</summary>
public partial class Tokentotaal : UserControl
{
    public Tokentotaal()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Telt de weken op tot één totaal. Gebeurt hier en niet bij de broker:
    /// die levert de weken al, en het totaal is niets anders dan de som
    /// daarvan - een aparte vraag aan de database zou hetzelfde antwoord
    /// twee keer laten uitrekenen.
    /// </summary>
    public void Show(IReadOnlyList<TokenUsageWeek> weken)
    {
        ArgumentNullException.ThrowIfNull(weken);

        var totaal = weken.Sum(week => week.TotalTokens);
        var prompt = weken.Sum(week => week.PromptTokens);
        var antwoord = weken.Sum(week => week.CompletionTokens);
        var aanroepen = weken.Sum(week => week.Calls);

        TotaalText.Text = totaal.ToString("N0", CultureInfo.CurrentCulture);

        Onderschrift.Text = aanroepen == 0
            ? "Nog niets geteld"
            : string.Format(
                CultureInfo.CurrentCulture,
                "{0:N0} prompt · {1:N0} antwoord · {2:N0} {3} · {4} {5}",
                prompt,
                antwoord,
                aanroepen,
                aanroepen == 1 ? "aanroep" : "aanroepen",
                weken.Count,
                weken.Count == 1 ? "week" : "weken");
    }
}
