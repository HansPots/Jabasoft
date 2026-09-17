using System.Windows;
using System.Windows.Controls;
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
}
