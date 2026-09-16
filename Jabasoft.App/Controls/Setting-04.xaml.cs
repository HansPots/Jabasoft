using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-04.xaml - see that file for what it looks like.</summary>
public partial class Setting04 : UserControl
{
    public Setting04()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Zet de nieuwe waarde door naar de blokken. De doeldictionary komt
    /// van AppSettingsScope: in een echte app is dat Application.Resources
    /// (alles kleurt mee), in het Stylebook alleen het previewvlak - zodat
    /// het bekijken van deze kaart de tool eromheen niet verbouwt.
    /// </summary>
    private void GapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Vuurt al tijdens InitializeComponent, voordat GapLabel bestaat.
        if (GapLabel is null)
        {
            return;
        }

        var pixels = (int)Math.Round(e.NewValue);
        GapLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "Space around each block (header, menu, content, action, footer) \u2014 {0} px",
            pixels);

        LayoutManager.ApplyRegionGap(pixels, AppSettingsScope.NearestThemed(this));
    }

    private void ResetGap_Click(object sender, RoutedEventArgs e) =>
        GapSlider.Value = LayoutManager.DefaultGap;
}
