using System.Windows;
using System.Windows.Controls;

namespace Jabasoft.App.Controls;

/// <summary>Welke knop in het menu is aangeklikt - zie <see cref="Navigatiemenu.ItemSelected"/>.</summary>
public enum NavigatiemenuItem
{
    /// <summary>Terug naar het hoofdscherm van de app.</summary>
    Main,

    /// <summary>De Stylebook-applicatie openen.</summary>
    Stylebook,

    /// <summary>Het instellingenscherm van de app.</summary>
    Settings,
}

/// <summary>Interaction logic for Navigatiemenu.xaml - see that file for what it looks like.</summary>
public partial class Navigatiemenu : UserControl
{
    /// <summary>
    /// Gaat af als er op een menuknop geklikt is. Het menu doet zelf NIETS
    /// met die klik: het weet niet welke schermen een app heeft en moet dat
    /// ook niet weten, anders is het geen gedeeld component meer. De app
    /// die dit menu plaatst luistert hierop en wijst zijn eigen bestemming
    /// aan - zie Jabasoft.App/MainWindow.xaml.cs.
    /// </summary>
    public event EventHandler<NavigatiemenuItem>? ItemSelected;

    public Navigatiemenu()
    {
        InitializeComponent();
    }

    /// <summary>De versietekst onderin de balk - elke app zet hier zijn eigen versienummer in.</summary>
    public string Version
    {
        get => VersionText.Text;
        set => VersionText.Text = value;
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        var item = sender switch
        {
            _ when ReferenceEquals(sender, StylebookButton) => NavigatiemenuItem.Stylebook,
            _ when ReferenceEquals(sender, SettingsButton) => NavigatiemenuItem.Settings,
            _ => NavigatiemenuItem.Main,
        };

        ItemSelected?.Invoke(this, item);
    }
}
