using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.EntityFrameworkCore;
using Stylebook.Data;

namespace Jabasoft.App;

/// <summary>
/// Interaction logic for SettingsView.xaml
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// Zelfde grens als Stylebook.Playground's MaxXamlElementCount -
    /// XamlReader.Parse en WPF's layoutmotor zijn beide recursief, een
    /// kapotte/te diep geneste XAML kan de stack laten overlopen (niet
    /// vangbaar, de hele app zou meegaan).
    /// </summary>
    private const int MaxXamlElementCount = 500;

    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Elke keer opnieuw geladen (niet alleen in de constructor) - zodra
    /// je een stylewijziging in de Stylebook opslaat en hier terugkomt,
    /// zie je 'm meteen, zonder Jabasoft.App te hoeven herstarten.
    /// </summary>
    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        Host.Content = LoadContent();
    }

    /// <summary>
    /// Leest het "Instellingen"-component live uit dezelfde
    /// JabasoftStylebook-database als Stylebook.Playground en rendert de
    /// opgeslagen Xaml - zelfde veilige aanpak als Playground's
    /// RenderXamlPreview: een kapotte, te grote, of onbereikbare
    /// database crasht Jabasoft.App nooit, toont in plaats daarvan een
    /// duidelijke melding.
    /// </summary>
    private static FrameworkElement LoadContent()
    {
        string xaml;
        try
        {
            var connectionString = ReadConnectionString();
            var options = new DbContextOptionsBuilder<StylebookDbContext>().UseSqlServer(connectionString).Options;
            using var db = new StylebookDbContext(options);
            var component = db.Components.AsNoTracking().FirstOrDefault(c => c.Name == "Instellingen");

            if (string.IsNullOrWhiteSpace(component?.Xaml))
            {
                return Placeholder("Component 'Instellingen' niet gevonden in Stylebook.");
            }

            xaml = component.Xaml;
        }
        catch (Exception ex)
        {
            return Placeholder($"Kon de Stylebook-database niet bereiken: {ex.Message}");
        }

        if (xaml.Count(c => c == '<') > MaxXamlElementCount)
        {
            return Placeholder($"'Instellingen' is te groot/diep genest om veilig te tonen (meer dan {MaxXamlElementCount} elementen).");
        }

        try
        {
            return XamlReader.Parse(xaml) as FrameworkElement ?? Placeholder("'Instellingen' is geen FrameworkElement.");
        }
        catch (Exception ex)
        {
            return Placeholder($"XAML-fout in 'Instellingen': {ex.Message}");
        }
    }

    private static string ReadConnectionString()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("ConnectionStrings").GetProperty("JabasoftStylebook").GetString()
            ?? throw new InvalidOperationException("ConnectionStrings:JabasoftStylebook ontbreekt in appsettings.json.");
    }

    private static FrameworkElement Placeholder(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        block.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
        return block;
    }
}
