using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Layout;
using Jabasoft.App.Taal;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-04.xaml - see that file for what it looks like.</summary>
public partial class Setting04 : UserControl
{
    /// <summary>Aan terwijl de kaart zichzelf op de bewaarde stand zet.</summary>
    private bool _laden;

    public Setting04()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += (_, _) => LanguageManager.Changed -= OnTaalGewisseld;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LanguageManager.Changed -= OnTaalGewisseld;
        LanguageManager.Changed += OnTaalGewisseld;

        _laden = true;
        try
        {
            GapSlider.Value = Preferences.Tussenruimte;
        }
        finally
        {
            _laden = false;
        }

        Toon(GapSlider.Value);
    }

    private void OnTaalGewisseld(object? sender, EventArgs e) => Toon(GapSlider.Value);

    /// <summary>De regel boven de schuifbalk, in de taal van nu.</summary>
    private void Toon(double waarde)
    {
        if (GapLabel is null)
        {
            return;
        }

        var sjabloon = TryFindResource("T_SpacingFormaat") as string
            ?? "Space around each block (header, menu, content, action, footer) — {0} px";

        GapLabel.Text = string.Format(CultureInfo.CurrentCulture, sjabloon, (int)Math.Round(waarde));
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
        Toon(pixels);

        // Zie Setting-03: tijdens het bouwen van de kaart alleen het
        // opschrift bijwerken, niets toepassen en niets bewaren.
        if (!IsLoaded)
        {
            return;
        }

        LayoutManager.ApplyRegionGap(pixels, AppSettingsScope.NearestThemed(this));

        if (!_laden)
        {
            Preferences.BewaarTussenruimte(pixels);
        }
    }

    private void ResetGap_Click(object sender, RoutedEventArgs e) =>
        GapSlider.Value = LayoutManager.DefaultGap;
}
