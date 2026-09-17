using System.Windows.Controls;

namespace Jabasoft.App.Regios.Actie;

/// <summary>Interaction logic for Actiebalk.xaml - see that file for what it looks like.</summary>
public partial class Actiebalk : UserControl
{
    public Actiebalk()
    {
        InitializeComponent();

        RefreshAction.Clicked += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>De verversknop is aangeklikt.</summary>
    public event EventHandler? RefreshRequested;
}
