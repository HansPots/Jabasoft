using System.Windows;
using System.Windows.Controls;
using Jabasoft.App.Layout;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-01.xaml - see that file for what it looks like.</summary>
public partial class Setting01 : UserControl
{
    /// <summary>Aan terwijl de kaart zichzelf op de bewaarde stand zet - dan mag een vinkje niets uitlokken.</summary>
    private bool _laden;

    public Setting01()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Zet de knoppen op het thema dat bij het opstarten al toegepast is
    /// (zie Layout/Preferences). Zonder dit zegt de kaart altijd LCARS,
    /// ook als de app in het andere thema opgekomen is - en dan doet
    /// klikken op LCARS niets.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _laden = true;
        try
        {
            if (Preferences.Thema == Theme.VisualStudio)
            {
                ThemeVsCode.IsChecked = true;
            }
            else
            {
                ThemeLcars.IsChecked = true;
            }
        }
        finally
        {
            _laden = false;
        }
    }

    /// <summary>
    /// Wisselt het thema van de hele omgeving.
    ///
    /// De IsLoaded-grendel is nodig, geen overdaad: ThemeLcars staat in de
    /// XAML op IsChecked="True" en dat vuurt Checked al terwijl de XAML
    /// wordt ingeladen. Deze kaart hangt op dat moment nog nergens in een
    /// venster, dus AppSettingsScope zou uitkomen bij Application.Resources
    /// - het enkel TONEN van deze kaart zou dan de hele omgeving omkleuren.
    ///
    /// Checked en niet Click, zodat het ook werkt als je met de pijltjes of
    /// de spatiebalk van knop wisselt.
    /// </summary>
    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _laden)
        {
            return;
        }

        var target = AppSettingsScope.NearestThemed(this);
        var theme = ReferenceEquals(sender, ThemeVsCode) ? Theme.VisualStudio : Theme.Lcars;
        ThemeManager.Apply(theme, target);

        // De hoekstralen komen uit het thema en zijn dus net veranderd; de
        // binnenstraal van de infoblok-titelbalk wordt daaruit afgeleid en
        // moet opnieuw berekend worden.
        LayoutManager.RefreshInnerRadius(target);

        Preferences.BewaarThema(theme);
    }
}
