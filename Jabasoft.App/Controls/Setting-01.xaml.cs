using System.Windows;
using System.Windows.Controls;
using Stylebook.Components.Theming;

namespace Jabasoft.App.Controls;

/// <summary>Interaction logic for Setting-01.xaml - see that file for what it looks like.</summary>
public partial class Setting01 : UserControl
{
    public Setting01()
    {
        InitializeComponent();
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
        if (!IsLoaded)
        {
            return;
        }

        var theme = ReferenceEquals(sender, ThemeVsCode) ? Theme.VisualStudio : Theme.Lcars;
        ThemeManager.Apply(theme, AppSettingsScope.NearestThemed(this));
    }
}
