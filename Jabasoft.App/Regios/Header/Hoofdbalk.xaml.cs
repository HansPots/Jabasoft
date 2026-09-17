using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Jabasoft.App.Layout;

namespace Jabasoft.App.Regios.Header;

/// <summary>Interaction logic for Hoofdbalk.xaml - see that file for what it looks like.</summary>
public partial class Hoofdbalk : UserControl
{
    /// <summary>Waaronder de kolombreedtes van de infoblokken bewaard worden.</summary>
    private const string LayoutKey = "header.blocks";

    public Hoofdbalk()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    /// <summary>Zet de breedtes terug die de gebruiker de vorige keer versleept heeft.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e) => SplitterLayout.Restore(BlockRow, LayoutKey);

    /// <summary>
    /// Bewaren zodra het slepen klaar is, niet tijdens. Anders schrijf je
    /// tientallen keren per seconde naar schijf voor een beweging die de
    /// gebruiker misschien nog terugdraait.
    /// </summary>
    private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        SplitterLayout.Remember(BlockRow, LayoutKey);

    /// <summary>Alle blokken weer even breed, en het bewaarde gesleep vergeten.</summary>
    public void ResetLayout() => SplitterLayout.Reset(BlockRow, LayoutKey);

    /// <summary>
    /// De bovenste strook van de header doet dienst als titelbalk: slepen
    /// verplaatst het venster, dubbelklikken maximaliseert het of zet het
    /// terug. Het venster heeft er zelf geen meer (MainWindow staat op
    /// WindowStyle="None") en WindowChrome laat het slepen bewust aan ons
    /// over, want alleen deze strook mag het doen.
    ///
    /// "Deze strook" = alles BOVEN de rij met infoblokken: de kleurbalk en
    /// het beeldmerk dat eroverheen ligt. Daaronder zitten de splitters
    /// tussen de blokken, en die moet je juist kunnen pakken. De
    /// vensterknopjes rechts vallen er ook buiten - een Button verwerkt
    /// zijn eigen muisklik, dus die bereikt dit raster niet.
    /// </summary>
    private void Titelstrook_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(BlockRow).Y >= 0)
        {
            return;
        }

        var venster = Window.GetWindow(this);
        if (venster is null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            venster.WindowState = venster.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        // Niet slepen als het venster gemaximaliseerd is: DragMove zou het
        // dan over het scherm trekken zonder het eerst terug te zetten, en
        // dat ziet er raar uit. Dubbelklikken zet het terug, daarna is het
        // gewoon weer te verslepen.
        if (venster.WindowState != WindowState.Maximized)
        {
            venster.DragMove();
        }
    }
}
