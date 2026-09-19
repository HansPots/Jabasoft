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

    /// <summary>LocalAiStudio openen.</summary>
    AiStudio,

    /// <summary>Het tokenverbruik van de AI-aanroepen.</summary>
    Tokens,

    /// <summary>Het gezondheidsoverzicht: wat er wel en niet draait.</summary>
    Health,

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

        ToonActief();
    }

    private NavigatiemenuItem _actief = NavigatiemenuItem.Main;

    /// <summary>
    /// Welke pagina er nu open staat. De applicatie zet dit; het menu weet
    /// zelf niet waar een knop naartoe leidt - zie de toelichting bij
    /// ItemSelected.
    ///
    /// Stylebook staat er bewust niet tussen als blijvende stand: die knop
    /// start een andere applicatie en verandert het inhoudsvak niet. Zou hij
    /// gemarkeerd blijven, dan lijkt het of je "in" Stylebook zit terwijl je
    /// naar het hoofdscherm kijkt.
    /// </summary>
    public NavigatiemenuItem Active
    {
        get => _actief;
        set
        {
            // Stylebook en AI Studio zijn andere applicaties: die blijven
            // nooit als stand staan.
            if (value is NavigatiemenuItem.Stylebook or NavigatiemenuItem.AiStudio || _actief == value)
            {
                return;
            }

            _actief = value;
            ToonActief();
        }
    }

    /// <summary>
    /// De actieve knop wordt omgekeerd: zwart vlak met een accentrand en
    /// accenttekst, in plaats van een vol accentvlak met zwarte tekst.
    /// Dezelfde taal als de keuzeknoppen op de instellingenkaarten, waar
    /// "gekozen" er ook zo uitziet.
    ///
    /// Met SetResourceReference en niet met een vaste kleur: dan blijft het
    /// meelopen als je van thema wisselt terwijl deze pagina open staat.
    /// </summary>
    private void ToonActief()
    {
        Zet(MainButton, _actief == NavigatiemenuItem.Main);
        Zet(TokensButton, _actief == NavigatiemenuItem.Tokens);
        Zet(HealthButton, _actief == NavigatiemenuItem.Health);
        Zet(SettingsButton, _actief == NavigatiemenuItem.Settings);

        // Stylebook en AI Studio starten een andere applicatie: dat zijn
        // handelingen, geen bestemmingen - dus nooit gemarkeerd.
        Zet(StylebookButton, false);
        Zet(AiStudioButton, false);
    }

    private static void Zet(Button knop, bool actief)
    {
        knop.SetResourceReference(BackgroundProperty, actief ? "BackgroundBrush" : "AccentBrush");
        knop.SetResourceReference(ForegroundProperty, actief ? "AccentBrush" : "AccentForegroundBrush");
        knop.BorderThickness = new Thickness(actief ? 2 : 0);
    }

    /// <summary>
    /// De versietekst onderin de balk - elke app zet hier zijn eigen
    /// versienummer in. Het menu verzint of zoekt het niet zelf op: het weet
    /// niet in welke applicatie het hangt.
    /// </summary>
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
            _ when ReferenceEquals(sender, AiStudioButton) => NavigatiemenuItem.AiStudio,
            _ when ReferenceEquals(sender, TokensButton) => NavigatiemenuItem.Tokens,
            _ when ReferenceEquals(sender, HealthButton) => NavigatiemenuItem.Health,
            _ when ReferenceEquals(sender, SettingsButton) => NavigatiemenuItem.Settings,
            _ => NavigatiemenuItem.Main,
        };

        ItemSelected?.Invoke(this, item);
    }
}
