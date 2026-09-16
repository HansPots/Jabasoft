using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-05.xaml - see that file for what it looks like.</summary>
public partial class Setting05 : UserControl
{
    public Setting05()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Zet de nieuwe randdikte door. De doeldictionary komt van
    /// AppSettingsScope, net als bij de andere kaarten: in een echte app is
    /// dat Application.Resources, in het Stylebook alleen het previewvlak.
    /// </summary>
    private void BorderSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Vuurt al tijdens InitializeComponent, voordat BorderLabel bestaat.
        if (BorderLabel is null)
        {
            return;
        }

        var pixels = (int)Math.Round(e.NewValue);
        BorderLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "Border around cards and info blocks \u2014 {0} px",
            pixels);

        LayoutManager.ApplyRegionBorder(pixels, AppSettingsScope.NearestThemed(this));
    }

    private void ResetBorder_Click(object sender, RoutedEventArgs e) =>
        BorderSlider.Value = LayoutManager.DefaultBorder;
}
