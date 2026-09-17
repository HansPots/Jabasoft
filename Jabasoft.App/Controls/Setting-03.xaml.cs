using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Layout;
using Jabasoft.App.Taal;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-03.xaml - see that file for what it looks like.</summary>
public partial class Setting03 : UserControl
{
    /// <summary>Gelijk aan de startwaarde van de schuifbalk en aan de terugval in Statusbalk.</summary>
    public const double DefaultIntervalSeconds = 5;

    /// <summary>Aan terwijl de kaart zichzelf op de bewaarde stand zet.</summary>
    private bool _laden;

    public Setting03()
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
            IntervalSlider.Value = Preferences.Ververstijd;
        }
        finally
        {
            _laden = false;
        }

        Toon(IntervalSlider.Value);
    }

    private void OnTaalGewisseld(object? sender, EventArgs e) => Toon(IntervalSlider.Value);

    /// <summary>De regel boven de schuifbalk, in de taal van nu.</summary>
    private void Toon(double waarde)
    {
        if (IntervalLabel is null)
        {
            return;
        }

        var sjabloon = TryFindResource("T_RefreshFormaat") as string
            ?? "Footer refresh time (CPU/RAM/Model VRAM) — {0} sec";

        IntervalLabel.Text = string.Format(CultureInfo.CurrentCulture, sjabloon, (int)Math.Round(waarde));
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
        Toon(seconds);

        // Nog niet in een venster? Dan alleen het opschrift. Deze gebeurtenis
        // vuurt namelijk al tijdens InitializeComponent, met de waarde die in
        // de XAML staat - en die zou dan de bewaarde voorkeur overschrijven,
        // zowel op het scherm als in settings.json.
        if (!IsLoaded)
        {
            return;
        }

        AppSettingsScope.NearestThemed(this)["RefreshIntervalSeconds"] = (double)seconds;

        if (!_laden)
        {
            Preferences.BewaarVerverstijd(seconds);
        }
    }

    private void ResetInterval_Click(object sender, RoutedEventArgs e) =>
        IntervalSlider.Value = DefaultIntervalSeconds;
}
