using Jabasoft.Base.AiBroker;

namespace Jabasoft.App.Controls;

/// <summary>
/// De groene en gele ster van een model in een keuzelijst, net als in de
/// modeltabel (Setting10): groen = samen vastgesteld (Aanbevelingen), geel =
/// eigen favoriet (AiServerSettings.FavorietenPerOnderdeel). Elke keuzelijst
/// hoort bij een of meer onderdelen: de Goose-kaart bij Goose, de
/// Hermes-kaart bij Hermes, de AI-kaart bij de apps die die modellen
/// gebruiken (LocalAiStudio en ImageFunn).
/// </summary>
internal static class Onderdeelsterren
{
    public static (bool Groen, bool Geel) Voor(string naam, AiServerSettings? instellingen, params string[] onderdelen) =>
        (onderdelen.Any(onderdeel => Aanbevelingen.Voor(naam, onderdeel).Count > 0),
         instellingen is not null && onderdelen.Any(onderdeel => instellingen.IsFavoriet(naam, onderdeel)));
}

/// <summary>
/// Eén regel in een keuzelijst op de Hermes- of Goose-kaart: naam, info
/// (context, doel), de kwaliteitssterren en de groene/gele ster. ToString is
/// de naam, zodat een bewerkbare keuzelijst de naam als tekst neemt.
/// </summary>
internal sealed record ModelRegel(string Naam, string Info, string Kwaliteit = "", bool Groen = false, bool Geel = false)
{
    public bool HeeftSter => Groen || Geel;

    public override string ToString() => Naam;
}
