using System.Windows;

namespace Jabasoft.App.Taal;

/// <summary>De talen die het scherm kent.</summary>
public enum AppLanguage
{
    Nederlands,
    English,
}

/// <summary>
/// Wisselt de schermtaal om terwijl de applicatie draait, op precies
/// dezelfde manier als ThemeManager een thema omwisselt: het hele
/// woordenboek Taal/Strings.&lt;taal&gt;.xaml wordt vervangen in de
/// MergedDictionaries van de doeldictionary.
///
/// Waarom zo en niet met de gebruikelijke .resx-bestanden: elke tekst op
/// het scherm staat als {DynamicResource T_...} in de XAML, en een
/// DynamicResource kijkt opnieuw op zodra de dictionary verandert. Het
/// scherm klapt dus meteen om, zonder herstart en zonder dat er ergens
/// een scherm opnieuw opgebouwd hoeft te worden.
///
/// Net als bij het thema moet de vervanging op het EIGEN niveau van de
/// doeldictionary gebeuren; een woordenboek dat dieper genest zit
/// verwisselen werkt niet betrouwbaar door naar wat er al op het scherm
/// staat.
/// </summary>
public static class LanguageManager
{
    private const string TaalMapPath = "Taal/";
    private const string TaalPackPrefix = "pack://application:,,,/Taal/";

    /// <summary>
    /// Gaat af nadat de taal gewisseld is.
    ///
    /// Teksten die in de XAML staan hebben dit niet nodig - die klappen
    /// vanzelf om. Dit is voor de regels die in code worden SAMENGESTELD,
    /// zoals "Tekstgrootte (16 px)": die zijn op het moment van wisselen al
    /// gezet en blijven anders in de oude taal staan tot je de schuifbalk
    /// aanraakt.
    /// </summary>
    public static event EventHandler? Changed;

    /// <summary>De bestandsnaam per taal - kort, zoals het in settings.json terechtkomt.</summary>
    private static string Bestandsdeel(AppLanguage taal) => taal == AppLanguage.English ? "en" : "nl";

    public static void Apply(AppLanguage taal, ResourceDictionary target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var vervanger = new ResourceDictionary
        {
            Source = new Uri($"{TaalPackPrefix}Strings.{Bestandsdeel(taal)}.xaml", UriKind.Absolute),
        };

        var merged = target.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source?.OriginalString.Contains(TaalMapPath, StringComparison.OrdinalIgnoreCase) == true)
            {
                merged[i] = vervanger;
                Changed?.Invoke(null, EventArgs.Empty);
                return;
            }
        }

        // Nog geen taalwoordenboek aanwezig: er dan een bij zetten in plaats
        // van klagen. Zo kan een scherm dat los bekeken wordt (de
        // ontwerpweergave) ook aan zijn teksten komen.
        merged.Add(vervanger);
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
