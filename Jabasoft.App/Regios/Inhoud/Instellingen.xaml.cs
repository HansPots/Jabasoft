using System.Windows.Controls;

namespace Jabasoft.App.Regios.Inhoud;

/// <summary>Interaction logic for Instellingen.xaml - see that file for what it looks like.</summary>
public partial class Instellingen : UserControl
{
    public Instellingen()
    {
        InitializeComponent();
    }

    /// <summary>
    /// De AI-kaart, zodat MainWindow zich op zijn SettingsSaved kan
    /// abonneren en de gezondheidscontrole opnieuw kan laten lopen. De
    /// kaart zelf weet niets van de footer of van de controle - hij meldt
    /// alleen dát er iets veranderd is.
    /// </summary>
    public Controls.Setting06 Ai => AiKaart;
}
