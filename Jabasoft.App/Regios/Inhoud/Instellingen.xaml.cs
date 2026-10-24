using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Jabasoft.App.Regios.Inhoud;

/// <summary>De kopjes op het instellingenscherm, een per onderdeel - zie <see cref="Instellingen.ToonSectie"/>.</summary>
public enum InstellingenSectie
{
    Algemeen,
    Ai,
    AiStudio,
    TabStudio,
    ImageFunn,
}

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

    /// <summary>
    /// Scrolt zo dat het kopje van <paramref name="sectie"/> bovenaan staat.
    /// Voor wie vanuit een onderdeel naar zijn instellingen komt (de
    /// Instellingen-knop in het menu van AI Studio).
    ///
    /// Via de Dispatcher: wie dit aanroept heeft de pagina meestal net in
    /// beeld gezet, en vóór de opmaak is er nog geen positie om naartoe te
    /// scrollen.
    /// </summary>
    public void ToonSectie(InstellingenSectie sectie)
    {
        FrameworkElement kop = sectie switch
        {
            InstellingenSectie.Ai => KopAi,
            InstellingenSectie.AiStudio => KopAiStudio,
            InstellingenSectie.TabStudio => KopTabStudio,
            InstellingenSectie.ImageFunn => KopImageFunn,
            _ => KopAlgemeen,
        };

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var positie = kop.TranslatePoint(new Point(0, 0), Kaarten).Y;
            Scroller.ScrollToVerticalOffset(Math.Max(0, positie - kop.Margin.Top));
        });
    }
}
