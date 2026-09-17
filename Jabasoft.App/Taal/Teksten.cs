using System.Globalization;
using System.Windows;

namespace Jabasoft.App.Taal;

/// <summary>
/// Haalt een schermtekst op uit het taalwoordenboek.
///
/// Voor teksten die in de XAML staan is dit niet nodig - die verwijzen er
/// met {DynamicResource} rechtstreeks naar. Dit is voor regels die in code
/// worden SAMENGESTELD, zoals "5 modellen gevonden op ...": die hebben een
/// sjabloon nodig dat per taal anders is.
///
/// Wordt een sleutel niet gevonden (een taalbestand dat achterloopt), dan
/// komt de meegegeven terugval op het scherm in plaats van een lege plek -
/// dat is te zien en te herstellen, een leeg vak niet.
/// </summary>
public static class Teksten
{
    public static string Van(FrameworkElement element, string sleutel, string terugval)
    {
        ArgumentNullException.ThrowIfNull(element);

        return element.TryFindResource(sleutel) as string ?? terugval;
    }

    /// <summary>Hetzelfde, maar meteen ingevuld met <paramref name="waarden"/>.</summary>
    public static string Vul(FrameworkElement element, string sleutel, string terugval, params object?[] waarden) =>
        string.Format(CultureInfo.CurrentCulture, Van(element, sleutel, terugval), waarden);
}
