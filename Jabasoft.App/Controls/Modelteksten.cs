using System.Windows;
using Jabasoft.App.Taal;

namespace Jabasoft.App.Controls;

/// <summary>
/// De namen van modelsoorten en taken in de taal van nu - gedeeld door de
/// AI-, Hermes- en Goose-kaart en de modeltabel, zodat overal hetzelfde
/// woord staat.
/// </summary>
internal static class Modelteksten
{
    public static string SoortNaam(FrameworkElement bron, Modelsoort soort) => soort switch
    {
        Modelsoort.Code => Teksten.Van(bron, "T_AiSoortCode", "code"),
        Modelsoort.Embedding => Teksten.Van(bron, "T_AiSoortEmbedding", "embedding"),
        _ => Teksten.Van(bron, "T_AiSoortAlgemeen", "general"),
    };

    public static string TaakNaam(FrameworkElement bron, Taak taak) => taak switch
    {
        Taak.Chat => Teksten.Van(bron, "T_AiTaakChat", "Chat"),
        Taak.Code => Teksten.Van(bron, "T_AiTaakCode", "Code"),
        Taak.Controle => Teksten.Van(bron, "T_AiTaakControleLang", "Check"),
        Taak.Beeld => Teksten.Van(bron, "T_AiTaakBeeld", "Image"),
        Taak.Zoeken => Teksten.Van(bron, "T_AiTaakZoeken", "Search"),
        _ => Teksten.Van(bron, "T_AiTaakAgent", "Agent"),
    };

    /// <summary>Een modelkeuze met grootte/soort, doel en - als LM Studio die gaf - context en beeldherkenning.</summary>
    public static Modelkeuze Keuze(FrameworkElement bron, string naam, int? sterren = null, bool? kanBeelden = null, int maxContext = 0) =>
        Modelkeuze.Van(naam, soort => SoortNaam(bron, soort), sterren, taak => TaakNaam(bron, taak), kanBeelden, maxContext);
}
