using System.Windows.Controls;
using System.Windows.Markup;

namespace Jabasoft.App.Paginas;

/// <summary>
/// Interaction logic for Hoofdscherm.xaml - see that file for which
/// slots are baked in (Header/Footer) versus passed through
/// (Menu/Inhoud/Actie). [ContentProperty(nameof(MainContent))] mirrors
/// Basis' own convention, so implicit XAML child content
/// (&lt;apps:Hoofdscherm&gt;...&lt;/apps:Hoofdscherm&gt;) lands in Inhoud,
/// the same way callers already expect from Basis itself.
/// </summary>
[ContentProperty(nameof(MainContent))]
public partial class Hoofdscherm : UserControl
{
    public Hoofdscherm()
    {
        InitializeComponent();
    }

    public object? MenuContent
    {
        get => Shell.MenuContent;
        set => Shell.MenuContent = value;
    }

    public object? MainContent
    {
        get => Shell.MainContent;
        set => Shell.MainContent = value;
    }

    public object? ActionContent
    {
        get => Shell.ActionContent;
        set => Shell.ActionContent = value;
    }
}
