using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-03.xaml - see that file for what it looks like.</summary>
public partial class Setting03 : UserControl
{
    /// <summary>Gelijk aan de startwaarde van de schuifbalk en aan de terugval in Statusbalk.</summary>
    public const double DefaultIntervalSeconds = 5;

    public Setting03()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Zet de nieuwe ververstijd neer als resource; de footer (Regios/
    /// Footer/Statusbalk) leest 'm daar op en stelt zijn poller erop in.
    /// Dezelfde weg als de andere kaarten nemen, via AppSettingsScope: in
    /// een echte app komt dat uit bij Application.Resources, in het
    /// Stylebook alleen bij het previewvlak.
    ///
    /// Geen directe verwijzing naar de footer, want deze kaart weet niet
    /// waar die hangt - en hoort dat ook niet te weten.
    /// </summary>
    private void IntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Vuurt al tijdens InitializeComponent, voordat IntervalLabel bestaat.
        if (IntervalLabel is null)
        {
            return;
        }

        var seconds = (int)Math.Round(e.NewValue);
        IntervalLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "Footer refresh time (CPU/RAM/Model VRAM) \u2014 {0} sec",
            seconds);

        AppSettingsScope.NearestThemed(this)["RefreshIntervalSeconds"] = (double)seconds;
    }

    private void ResetInterval_Click(object sender, RoutedEventArgs e) =>
        IntervalSlider.Value = DefaultIntervalSeconds;
}
