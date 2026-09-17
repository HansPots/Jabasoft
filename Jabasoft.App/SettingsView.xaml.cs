using System.Windows.Controls;

namespace Jabasoft.App;

/// <summary>
/// Interaction logic for SettingsView.xaml - see that file. Thin wrapper
/// around Stylebook.Components' Instellingen component, kept as its own
/// class so this is the natural place to add settings-specific chrome
/// (a back-button, a title) later without touching MainWindow.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>De instellingenpagina zelf - voor wie bij een van de kaarten moet zijn (zie MainWindow's gezondheidscontrole).</summary>
    public Regios.Inhoud.Instellingen Instellingen => Pagina;
}
