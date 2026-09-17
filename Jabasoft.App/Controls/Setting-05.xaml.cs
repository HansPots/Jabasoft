using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Layout;
using Jabasoft.App.Taal;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-05.xaml - see that file for what it looks like.</summary>
public partial class Setting05 : UserControl
{
    /// <summary>Aan terwijl de kaart zichzelf op de bewaarde stand zet.</summary>
    private bool _laden;

    public Setting05()
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
            BorderSlider.Value = Preferences.Kaderrand;
        }
        finally
        {
            _laden = false;
        }

        Toon(BorderSlider.Value);
    }

    private void OnTaalGewisseld(object? sender, EventArgs e) => Toon(BorderSlider.Value);

    /// <summary>De regel boven de schuifbalk, in de taal van nu.</summary>
    private void Toon(double waarde)
    {
        if (BorderLabel is null)
        {
            return;
        }

        var sjabloon = TryFindResource("T_BorderFormaat") as string
            ?? "Border around cards and info blocks — {0} px";

        BorderLabel.Text = string.Format(CultureInfo.CurrentCulture, sjabloon, (int)Math.Round(waarde));
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
        Toon(pixels);

        // Zie Setting-03: tijdens het bouwen van de kaart alleen het
        // opschrift bijwerken, niets toepassen en niets bewaren.
        if (!IsLoaded)
        {
            return;
        }

        LayoutManager.ApplyRegionBorder(pixels, AppSettingsScope.NearestThemed(this));

        if (!_laden)
        {
            Preferences.BewaarKaderrand(pixels);
        }
    }

    private void ResetBorder_Click(object sender, RoutedEventArgs e) =>
        BorderSlider.Value = LayoutManager.DefaultBorder;
}
