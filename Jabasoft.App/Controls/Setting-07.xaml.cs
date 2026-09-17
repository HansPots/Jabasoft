using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Layout;
using Jabasoft.App.Taal;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>
/// Interaction logic for Setting-07.xaml - see that file for what it looks like.
///
/// De taal wordt op dezelfde plek gezet als het thema: de dichtstbijzijnde
/// dictionary die meewisselt. In de app is dat Application.Resources, dus
/// klapt het hele scherm om; in de ontwerpweergave alleen wat eromheen
/// staat.
/// </summary>
public partial class Setting07 : UserControl
{
    /// <summary>Aan terwijl de kaart zichzelf op de bewaarde stand zet - dan mag een vinkje niets uitlokken.</summary>
    private bool _laden;

    public Setting07()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _laden = true;
        try
        {
            if (Preferences.Taal == AppLanguage.English)
            {
                TaalEngels.IsChecked = true;
            }
            else
            {
                TaalNederlands.IsChecked = true;
            }
        }
        finally
        {
            _laden = false;
        }
    }

    /// <summary>
    /// Checked en niet Click, zodat het ook werkt als je met de pijltjes of
    /// de spatiebalk van knop wisselt - zelfde afspraak als bij de
    /// themakiezer.
    /// </summary>
    private void Taal_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _laden)
        {
            return;
        }

        var taal = ReferenceEquals(sender, TaalEngels) ? AppLanguage.English : AppLanguage.Nederlands;

        LanguageManager.Apply(taal, AppSettingsScope.NearestThemed(this));
        Preferences.BewaarTaal(taal);
    }
}
