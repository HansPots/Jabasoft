using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jabasoft.App.Layout;
using Jabasoft.App.Taal;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-02.xaml - see that file for what it looks like.</summary>
public partial class Setting02 : UserControl
{
    private const string DefaultFontFamily = "Segoe UI";

    private const double DefaultFontSize = 16;

    /// <summary>Aan terwijl de kaart zichzelf op de bewaarde stand zet.</summary>
    private bool _laden;

    public Setting02()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += (_, _) => LanguageManager.Changed -= OnTaalGewisseld;
    }

    /// <summary>Zet de keuzelijst en de schuifbalk op wat er bewaard is.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LanguageManager.Changed -= OnTaalGewisseld;
        LanguageManager.Changed += OnTaalGewisseld;

        _laden = true;
        try
        {
            var naam = Preferences.Lettertype;
            for (var i = 0; i < FontPicker.Items.Count; i++)
            {
                if (FontPicker.Items[i] is ComboBoxItem { Content: string keuze } &&
                    string.Equals(keuze, naam, StringComparison.OrdinalIgnoreCase))
                {
                    FontPicker.SelectedIndex = i;
                    break;
                }
            }

            SizeSlider.Value = Preferences.Tekstgrootte;
        }
        finally
        {
            _laden = false;
        }

        ToonGrootte(SizeSlider.Value);
    }

    /// <summary>De regel boven de schuifbalk wordt in code samengesteld, dus die moet bij een taalwissel opnieuw gezet worden.</summary>
    private void OnTaalGewisseld(object? sender, EventArgs e) => ToonGrootte(SizeSlider.Value);

    private void ToonGrootte(double waarde)
    {
        if (SizeLabel is null)
        {
            return;
        }

        var sjabloon = TryFindResource("T_FontGrootteFormaat") as string ?? "Text size ({0} px)";
        SizeLabel.Text = string.Format(CultureInfo.CurrentCulture, sjabloon, (int)Math.Round(waarde));
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

        Preferences.BewaarLettertype(SelectedFontName(), Math.Round(SizeSlider.Value));
    }

    private string SelectedFontName() =>
        FontPicker.SelectedItem is ComboBoxItem { Content: string name } ? name : DefaultFontFamily;

    private void FontPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Vuurt al tijdens het inladen (SelectedIndex staat in de XAML) -
        // dan hangt deze kaart nog nergens en is er niets om te zetten.
        if (!IsLoaded || _laden)
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

        ToonGrootte(e.NewValue);

        if (IsLoaded && !_laden)
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
