namespace Jabasoft.App.Controls;

/// <summary>
/// De modellen die we SAMEN als beste keuze hebben vastgesteld (groene ster
/// in de modeltabel) - door te meten en te proberen, niet geschat op de naam
/// zoals de rest van de tabel. Bewust in code en niet instelbaar: dit is wat
/// er bewezen is. Komt er een nieuwe uitkomst bij, dan komt hij hier.
///
/// De eigen favorieten van de gebruiker (gele ster) staan los hiervan in de
/// AI-instellingen (AiServerSettings.FavorietenPerOnderdeel).
/// </summary>
internal static class Aanbevelingen
{
    /// <summary>
    /// Een vastgestelde keuze: welk model (een stukje van de naam), voor welke onderdelen (Jabasoft.Base.AiBroker.Onderdelen
    /// - daar komt de groene ster in de modeltabel), welke taak, en waarom.
    /// </summary>
    public sealed record Aanbeveling(string Deel, IReadOnlyList<string> Onderdelen, Taak Taak, string Reden);

    private static readonly Aanbeveling[] Lijst =
    [
        new("qwen3.6-35b-a3b", [Jabasoft.Base.AiBroker.Onderdelen.Goose], Taak.Agent,
            "Hoofdmodel voor Goose (november 2026). In de Puzzeltest de beste in Goose bij de agent-taken: rekent echt " +
            "met de tool, verzint niets, en bouwde de netste rekenmachine. Met de MoE-experts op de CPU (LM Studio: GPU " +
            "Offload 40, 'force MoE weights onto CPU' 40, Flash Attention) ~40 tokens/s op de RTX 4070 SUPER, ongeveer " +
            "drie keer sneller dan volledig op de GPU, en het past in 6 GB videogeheugen. Context via de broker: 65.536, " +
            "meegroeiend tot 131.072."),
        new("gemma-4-12b", [Jabasoft.Base.AiBroker.Onderdelen.Hermes], Taak.Chat,
            "Hoofdmodel voor Hermes (november 2026). In de Puzzeltest het sterkst in redeneren: slak, januari-meisje en " +
            "als enige de bootvraag meteen goed. Niet voor bouwen: in Hermes bleef de rekenmachine een leeg sjabloon en " +
            "meldde het toch een volledige app - controleer wat het zegt gedaan te hebben. Past helemaal op de videokaart."),
        new("gemma-4-12b", [Jabasoft.Base.AiBroker.Onderdelen.LocalAiStudio, Jabasoft.Base.AiBroker.Onderdelen.ImageFunn], Taak.Chat,
            "Chatmodel voor LocalAiStudio en ImageFunn (november 2026): het sterkst in redeneren in de Puzzeltest, past " +
            "helemaal op de videokaart (7 GB) en is dus snel genoeg voor gewone vragen. In ImageFunn vertaalt het de " +
            "Nederlandse prompt naar Engels voor het beeldmodel. Nog niet apart in deze apps vergeleken."),
        new("bge-m3", [Jabasoft.Base.AiBroker.Onderdelen.LocalAiStudio], Taak.Zoeken,
            "Embeddingmodel voor het zoeken in LocalAiStudio: meertalig, dus ook Nederlandse tekst, en klein (0,6 GB). " +
            "Het andere embeddingmodel (nomic) leest alleen Engels goed."),
        new("juggernaut-xl-ragnarok", [Jabasoft.Base.AiBroker.Onderdelen.ImageFunn], Taak.Beeld,
            "Het beeldmodel van ImageFunn (standaard sinds oktober 2026): ImageFunn leest het rechtstreeks uit de " +
            "LM Studio-map. Geen chatmodel - daarom staat het in de modeltabel op niet tonen."),
    ];

    /// <summary>Wat er voor dit model is vastgesteld - leeg als niets.</summary>
    public static IReadOnlyList<Aanbeveling> Voor(string model) =>
        [.. Lijst.Where(aanbeveling => model.Contains(aanbeveling.Deel, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Wat er voor dit model bij dit onderdeel is vastgesteld - leeg als niets.</summary>
    public static IReadOnlyList<Aanbeveling> Voor(string model, string onderdeel) =>
        [.. Voor(model).Where(aanbeveling => aanbeveling.Onderdelen.Contains(onderdeel, StringComparer.Ordinal))];
}
