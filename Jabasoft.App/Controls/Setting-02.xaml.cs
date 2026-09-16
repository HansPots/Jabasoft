using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-02.xaml - see that file for what it looks like.</summary>
public partial class Setting02 : UserControl
{
    private const string DefaultFontFamily = "Segoe UI";

    private const double DefaultFontSize = 16;

    public Setting02()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Lettertype en tekstgrootte gaan NIET via een resource maar via
    /// overerving: ze worden op het venster gezet, en alles eronder dat
    /// zelf geen FontFamily/FontSize heeft staan neemt dat over. Dat is
    /// ook precies waarom het beeldmerk zijn eigen LogoFontFamily heeft -
    /// dat moet hier juist NIET in meegaan.
    /// </summary>
    private void ApplyFont()
    {
        if (AppSettingsScope.NearestRoot(this) is not { } root)
        {
            return;
        }

        root.FontFamily = new FontFamily(SelectedFontName());
        root.FontSize = Math.Round(SizeSlider.Value);
    }

    private string SelectedFontName() =>
        FontPicker.SelectedItem is ComboBoxItem { Content: string name } ? name : DefaultFontFamily;

    private void FontPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Vuurt al tijdens het inladen (SelectedIndex staat in de XAML) -
        // dan hangt deze kaart nog nergens en is er niets om te zetten.
        if (!IsLoaded)
        {
            return;
        }

        ApplyFont();
    }

    private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SizeLabel is null)
        {
            return;
        }

        SizeLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "Text size ({0} px)",
            (int)Math.Round(e.NewValue));

        if (IsLoaded)
        {
            ApplyFont();
        }
    }

    private void ResetFont_Click(object sender, RoutedEventArgs e)
    {
        FontPicker.SelectedIndex = 0;
        SizeSlider.Value = DefaultFontSize;
        ApplyFont();
    }
}
