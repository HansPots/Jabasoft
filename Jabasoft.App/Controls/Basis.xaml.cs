using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Jabasoft.App.Controls;

/// <summary>
/// The standard app-shell layout - header, footer, left menu and right
/// action rail around a center content area - see Basis.xaml for the
/// full explanation of its five content slots.
///
/// None of the five slots is named "Content": that inherited
/// ContentControl property is already spoken for internally
/// (InitializeComponent assigns Basis.xaml's own row/column Grid to it),
/// so a caller-set Content would silently replace the whole layout
/// instead of filling one region of it - see the note on Card.Body for
/// the same reasoning applied to a single-slot component.
/// [ContentProperty(nameof(MainContent))] routes implicit XAML child
/// content (&lt;controls:Basis&gt;...&lt;/controls:Basis&gt;) to the
/// center region, the same way most callers expect the "content between
/// the tags" to land.
/// </summary>
[ContentProperty(nameof(MainContent))]
public partial class Basis : UserControl
{
    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
        nameof(HeaderContent), typeof(object), typeof(Basis), new PropertyMetadata(null));

    public static readonly DependencyProperty MenuContentProperty = DependencyProperty.Register(
        nameof(MenuContent), typeof(object), typeof(Basis), new PropertyMetadata(null));

    public static readonly DependencyProperty MainContentProperty = DependencyProperty.Register(
        nameof(MainContent), typeof(object), typeof(Basis), new PropertyMetadata(null));

    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(
        nameof(ActionContent), typeof(object), typeof(Basis), new PropertyMetadata(null));

    public static readonly DependencyProperty FooterContentProperty = DependencyProperty.Register(
        nameof(FooterContent), typeof(object), typeof(Basis), new PropertyMetadata(null));

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public object? MenuContent
    {
        get => GetValue(MenuContentProperty);
        set => SetValue(MenuContentProperty, value);
    }

    public object? MainContent
    {
        get => GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public object? FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    public Basis()
    {
        InitializeComponent();
    }
}
