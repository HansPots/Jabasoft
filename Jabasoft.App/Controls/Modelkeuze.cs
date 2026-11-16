using System.Globalization;
using System.Text.RegularExpressions;

namespace Jabasoft.App.Controls;

/// <summary>Waar een model voor bedoeld is - afgeleid van de naam, want de servers zeggen het niet.</summary>
public enum Modelsoort
{
    Algemeen,
    Code,
    Embedding,
}

/// <summary>De taken waar een model voor ingezet kan worden - de kolommen van de modeltabel.</summary>
public enum Taak
{
    Chat,
    Code,
    Controle,
    Beeld,
    Zoeken,

    /// <summary>Als hoofdmodel van een agent (Hermes, Goose): plannen, tools gebruiken, lange context.</summary>
    Agent,
}

/// <summary>
/// Eén regel in een modelkeuzelijst van de AI-kaart: de naam waar het om
/// gaat, plus wat we er uit de NAAM over kunnen zeggen - grootte, soort en
/// een kwaliteitsschatting in sterren.
///
/// Alles komt uit de naam (zowel "qwen2.5-coder:14b" van Ollama als
/// "qwen2.5-coder-14b-instruct" van LM Studio), zodat de broker er niets
/// voor hoeft te leveren. De sterren zijn dus een SCHATTING op grootte en
/// soort - hoger betekent meestal beter en trager - geen benchmark.
/// </summary>
public sealed partial class Modelkeuze
{
    private Modelkeuze(string naam, double? miljardenParameters, string? kwantisatie, Modelsoort soort, int sterren, bool handmatig)
    {
        Naam = naam;
        MiljardenParameters = miljardenParameters;
        Kwantisatie = kwantisatie;
        Soort = soort;
        Sterren = sterren;
        Handmatig = handmatig;
    }

    public string Naam { get; }

    /// <summary>Het aantal parameters in miljarden, als de naam dat noemt (14b, 1.5b).</summary>
    public double? MiljardenParameters { get; }

    /// <summary>Bijvoorbeeld "Q4_K_M", als de naam dat noemt.</summary>
    public string? Kwantisatie { get; }

    public Modelsoort Soort { get; }

    /// <summary>0 tot en met 5. Een handmatige beoordeling (zie <see cref="Handmatig"/>) telt boven de schatting.</summary>
    public int Sterren { get; }

    /// <summary>Zijn de sterren door de gebruiker zelf gezet (AI-kaart), in plaats van een schatting op grootte?</summary>
    public bool Handmatig { get; }

    /// <summary>De sterren als tekst, bijvoorbeeld ★★★★☆.</summary>
    public string SterrenTekst => new string('★', Sterren) + new string('☆', 5 - Sterren);

    /// <summary>De grootte als tekst ("14B"), of leeg als de naam er niets over zegt.</summary>
    public string Grootte => MiljardenParameters is { } miljarden
        ? miljarden.ToString("0.#", CultureInfo.InvariantCulture) + "B"
        : string.Empty;

    /// <summary>De regel achter de naam: grootte, soort en eventueel kwantisatie.</summary>
    public string Info { get; private set; } = string.Empty;

    /// <summary>Geschatte inzetbaarheid per JabaSoft-taak (0-6) - zie <see cref="SchatGeschiktheid"/>.</summary>
    public Geschiktheid Taken { get; private set; } = new(0, 0, 0, 0, 0);

    /// <summary>Kan het model afbeeldingen lezen? Van LM Studio als dat bekend is, anders geschat op de naam.</summary>
    public bool KanBeelden { get; private set; }

    /// <summary>Denkt het model eerst na voor het antwoordt (reasoning)? Geschat op de naam.</summary>
    public bool Denkt { get; private set; }

    /// <summary>De grootste context die het model aankan (van LM Studio), of 0 als onbekend.</summary>
    public int MaxContext { get; private set; }

    /// <summary>
    /// Waar het model voor bedoeld is: de taken met de hoogste score (minstens
    /// 3, en hoogstens één punt onder de beste). Zie <see cref="DoelTekst"/>.
    /// </summary>
    public IReadOnlyList<Taak> BedoeldVoor { get; private set; } = [];

    /// <summary>"Bedoeld voor: Agent · Chat" in de taal van nu - leeg als Van geen taaknamen kreeg.</summary>
    public string DoelTekst { get; private set; } = string.Empty;

    public int Score(Taak taak) => taak switch
    {
        Taak.Chat => Taken.Chat,
        Taak.Code => Taken.Code,
        Taak.Controle => Taken.Controle,
        Taak.Beeld => Taken.Beeld,
        Taak.Zoeken => Taken.Zoeken,
        _ => Taken.Agent,
    };

    public override string ToString() => Naam;

    /// <summary>
    /// Maakt de keuze voor een naam. <paramref name="soortNaam"/> vertaalt
    /// een soort naar tekst in de taal van nu. <paramref name="handmatigeSterren"/>
    /// is de door de gebruiker ingestelde beoordeling (AiServerSettings.Sterren)
    /// - overschrijft de automatische schatting als hij er is.
    /// <paramref name="taakNaam"/> vertaalt een taak naar tekst; zonder blijft
    /// <see cref="DoelTekst"/> leeg. <paramref name="kanBeelden"/> en
    /// <paramref name="maxContext"/> komen van LM Studio als die bekend zijn
    /// (Hermes- en Goose-kaart, modeltabel) - beter dan raden op de naam.
    /// </summary>
    public static Modelkeuze Van(
        string naam,
        Func<Modelsoort, string> soortNaam,
        int? handmatigeSterren = null,
        Func<Taak, string>? taakNaam = null,
        bool? kanBeelden = null,
        int maxContext = 0)
    {
        var laag = naam.ToLowerInvariant();

        double? miljarden = null;

        if (Grootte_().Match(laag) is { Success: true } treffer
            && double.TryParse(treffer.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var waarde))
        {
            miljarden = waarde;
        }

        var kwantisatie = Kwantisatie_().Match(laag) is { Success: true } kwant ? kwant.Groups[1].Value.ToUpperInvariant() : null;

        var soort = laag.Contains("embed", StringComparison.Ordinal)
            ? Modelsoort.Embedding
            : Code_().IsMatch(laag) ? Modelsoort.Code : Modelsoort.Algemeen;

        var keuze = handmatigeSterren is { } gezet
            ? new Modelkeuze(naam, miljarden, kwantisatie, soort, Math.Clamp(gezet, 0, 5), handmatig: true)
            : new Modelkeuze(naam, miljarden, kwantisatie, soort, Schat(miljarden, kwantisatie, soort), handmatig: false);

        var delen = new List<string>();

        if (keuze.Grootte.Length > 0)
        {
            delen.Add(keuze.Grootte);
        }

        delen.Add(soortNaam(soort));

        if (kwantisatie is not null)
        {
            delen.Add(kwantisatie);
        }

        keuze.Info = string.Join(" · ", delen);
        keuze.KanBeelden = soort != Modelsoort.Embedding && (kanBeelden ?? Beeldherkenning_().IsMatch(laag));
        keuze.Denkt = soort != Modelsoort.Embedding && Redeneermodel_().IsMatch(laag);
        keuze.MaxContext = maxContext;
        keuze.Taken = SchatGeschiktheid(laag, miljarden, soort, keuze.KanBeelden, keuze.Denkt, maxContext);

        var beste = Enum.GetValues<Taak>().Max(keuze.Score);
        var drempel = Math.Max(3, beste - 1);
        keuze.BedoeldVoor = beste < 3
            ? []
            : [.. Enum.GetValues<Taak>().Where(taak => keuze.Score(taak) >= drempel).OrderByDescending(keuze.Score)];

        if (taakNaam is not null && keuze.BedoeldVoor.Count > 0)
        {
            keuze.DoelTekst = string.Join(" · ", keuze.BedoeldVoor.Select(taakNaam));
        }

        return keuze;
    }

    /// <summary>
    /// Schatting op grootte: hoe meer parameters, hoe beter (en trager).
    /// Een sterk ingekorte kwantisatie (Q2, Q3) kost een ster. Een model
    /// zonder bekende grootte krijgt drie sterren - niet te veel, niet te
    /// weinig. Embeddingmodellen zijn niet met chatmodellen te vergelijken;
    /// die krijgen ook drie.
    /// </summary>
    private static int Schat(double? miljarden, string? kwantisatie, Modelsoort soort)
    {
        if (soort == Modelsoort.Embedding || miljarden is null)
        {
            return 3;
        }

        var sterren = miljarden.Value switch
        {
            < 2 => 1,
            < 5 => 2,
            < 13 => 3,
            < 25 => 4,
            _ => 5,
        };

        if (kwantisatie is not null && (kwantisatie.StartsWith("Q2", StringComparison.Ordinal) || kwantisatie.StartsWith("Q3", StringComparison.Ordinal)))
        {
            sterren--;
        }

        return Math.Clamp(sterren, 0, 5);
    }

    [GeneratedRegex(@"(?<=^|[-:_/ ])(\d+(?:\.\d+)?)b(?![a-z0-9])")]
    private static partial Regex Grootte_();

    [GeneratedRegex(@"(?<![a-z0-9])(q\d(?:_[a-z0-9]+)*|f16|fp16|bf16)(?![a-z0-9])")]
    private static partial Regex Kwantisatie_();

    [GeneratedRegex(@"(?<![a-z0-9])(coder|code)(?![a-z0-9])")]
    private static partial Regex Code_();

    /// <summary>
    /// Bekende modelfamilies, herkend aan een stukje van de naam (op zijn
    /// LM Studio- én Ollama-vorm), met een korte Nederlandse notitie over
    /// waar zo'n model goed in is. Eerste treffer wint, dus specifiekere
    /// namen (zoals "qwen2.5-coder") staan boven de algemenere ("qwen2.5").
    /// Puur ter oriëntatie - geen benchmark, en wordt alleen gebruikt als
    /// het model nog geen eigen, zelf getypte notitie heeft (zie
    /// Setting-06.xaml.cs).
    /// </summary>
    private static readonly (string Deel, string Omschrijving)[] BekendeFamilies =
    [
        ("qwen3.6",
            "Nieuwste Qwen, als mixture of experts: 35B in totaal, maar per woord maar zo'n 3B actief - daardoor snel " +
            "voor zijn grootte. Sterk als agent: plant, gebruikt tools en houdt lange taken vol, en kan desgewenst " +
            "hardop nadenken.\n" +
            "Een goede keuze als hoofdmodel voor Hermes en Goose."),
        ("qwen3-vl",
            "Qwen3 met beeldherkenning: leest afbeeldingen, schermafbeeldingen en gescande documenten. Klein en snel - " +
            "bedoeld als beeldmodel naast een hoofdmodel, niet als hoofdmodel van een agent."),
        ("a3b-thinking",
            "Qwen3 30B (mixture of experts, ~3B actief) in de denk-variant: denkt altijd eerst uitgebreid na voor het " +
            "antwoordt. Sterk in redeneren, wiskunde en controleren; trager en kost meer tokens dan de gewone variant."),
        ("a3b-2507",
            "Qwen3 30B (mixture of experts, ~3B actief) in de instruct-variant: antwoordt meteen, zonder nadenken. " +
            "Snel, goed in chat en in tools gebruiken - bruikbaar als agentmodel."),
        ("gpt-oss",
            "OpenAI's open model (mixture of experts). Sterk in redeneren en in tools gebruiken, daardoor goed als " +
            "agent; denkt eerst na met instelbare inspanning. Leest geen afbeeldingen - geef het op de Hermes-kaart " +
            "een apart beeldmodel."),
        ("seed-oss",
            "ByteDance's 36B-model met een heel lange context en een instelbaar denkbudget. Sterk in redeneren, code " +
            "en agenttaken, maar groot: merkbaar trager op 12 GB videogeheugen."),
        ("magistral",
            "Mistral's redeneermodel (24B, op Mistral Small): denkt eerst na, sterk in meertalig redeneren en " +
            "controleren. Bruikbaar met tools, maar trager door het nadenken."),
        ("gemma-4",
            "Google's Gemma 4: compact en snel, leest ook afbeeldingen. Breed inzetbaar voor chat en beeld; als " +
            "agent minder sterk dan de grotere Qwen-modellen."),
        ("gemma-3",
            "Google's Gemma 3 - kan ook afbeeldingen lezen en beschrijven, vandaar geschikt als beeldmodel. Goed " +
            "algemeen taalgebruik, minder gespecialiseerd in code of tools."),
        ("deepseek-r1",
            "Redeneermodel (reasoning), gebouwd om te concurreren met OpenAI's o1/o3. Denkt eerst uitgebreid in zichzelf na " +
            "(chain of thought, via reinforcement learning aangeleerd) voordat het met een antwoord komt - waar een gewoon " +
            "model meteen begint te typen.\n" +
            "Sterk in wiskunde, logica en meerstaps-redeneren; ook goed in code. Nadeel: merkbaar trager door dat nadenken, " +
            "en dat gedachtespoor kost tokens (zie de instelling Denktijd hiernaast om dat te begrenzen)."),
        ("deepseek", "Sterk in code en wiskunde; over het algemeen goed voor zijn grootte."),
        ("qwen2.5-coder",
            "Toegespitst op programmeren: sterk in code schrijven, uitleggen, verbeteren en refactoren, over veel talen " +
            "heen (C#, Python, JavaScript, XAML, enzovoort).\n" +
            "Compact (bijvoorbeeld 14B) geldt het als een van de sterkste lokaal te draaien codeermodellen in zijn " +
            "gewichtsklasse - een goede standaardkeuze voor het codemodel in deze familie apps."),
        ("qwen2.5coder",
            "Toegespitst op programmeren: sterk in code schrijven, uitleggen, verbeteren en refactoren, over veel talen " +
            "heen. Compact en efficiënt voor zijn grootte."),
        ("qwen3-coder",
            "Nieuwere codeervariant van Qwen3: sterk in programmeren, met optioneel hardop nadenken (reasoning-modus) " +
            "voor lastige vraagstukken - dat kost dan wel extra tijd."),
        ("qwen3",
            "Kan desgewenst hardop nadenken (een aan/uit-schakelbare reasoning-modus) voor lastige vragen, en anders " +
            "meteen antwoorden zoals een gewoon model.\n" +
            "Sterk in wiskunde, logica en meertalige taken; recentere en over het algemeen krachtigere lichting dan " +
            "Qwen2.5."),
        ("qwen2.5",
            "Algemeen Qwen-model (geen aparte coder- of reasoning-variant): sterk in meertalige taken, wiskunde en " +
            "logisch redeneren. Compact en snel voor zijn grootte, en geldt binnen de AI-community als een van de " +
            "betere lokale modellen in zijn gewichtsklasse."),
        ("qwen", "Algemeen Qwen-model: doorgaans sterk in meertalige taken en redeneren voor zijn grootte."),
        ("gemma3",
            "Google's compacte model - kan ook afbeeldingen lezen en beschrijven (beeldherkenning), vandaar geschikt als " +
            "beeldmodel voor bijgevoegde plaatjes. Goed algemeen taalgebruik, minder gespecialiseerd in code dan een " +
            "coder-variant."),
        ("gemma", "Google's compacte, algemene model - snel en breed inzetbaar."),
        ("nomic-embed",
            "Embeddingmodel: zet tekst om in getallenreeksen (vectoren) voor semantisch zoeken - vindt bestanden op " +
            "betekenis, niet op exacte woorden. Niet bedoeld om zelf een gesprek mee te voeren of vragen aan te stellen."),
        ("embed", "Embeddingmodel: zet tekst om in vectoren voor zoeken - niet bedoeld voor een gewoon gesprek."),
        ("llama3", "Meta's algemene model: breed inzetbaar, prima startpunt voor gewone chat- en schrijftaken."),
        ("llama", "Meta's algemene model: breed inzetbaar voor gewone chat- en schrijftaken."),
        ("mixtral",
            "Meerdere kleinere modellen die samenwerken en per vraag de best passende erbij kiezen (mixture of " +
            "experts) - vaak sneller dan zijn totale grootte doet vermoeden, met een kwaliteit die in de buurt komt " +
            "van een groter, aaneengesloten model."),
        ("mistral", "Compact en snel algemeen model, sterk voor zijn grootte."),
        ("phi",
            "Microsoft's kleine model, getraind op zorgvuldig gekozen ('textbook-quality') data. Verrassend sterk in " +
            "redeneren en wiskunde voor zijn formaat, maar met minder brede wereldkennis dan een groter model."),
        ("starcoder", "Toegespitst op programmeren, getraind op veel broncode uit open-sourceprojecten."),
        ("codellama", "Meta's codeervariant van Llama: gericht op programmeren, met varianten voor Python en instructies."),
    ];

    /// <summary>
    /// Een korte, automatische notitie over waar dit model goed in is, op
    /// basis van bekende modelfamilies - of leeg als de naam niets herkends
    /// bevat. Alleen een SCHATTING op de naam, geen eigen mening; de
    /// gebruiker kan dit in het MODELLEN-blok altijd overschrijven met een
    /// eigen tekst.
    /// </summary>
    public static string AutomatischeOmschrijving(string naam)
    {
        var laag = naam.ToLowerInvariant();
        var gevonden = BekendeFamilies.FirstOrDefault(paar => laag.Contains(paar.Deel, StringComparison.Ordinal));
        return gevonden.Omschrijving ?? string.Empty;
    }

    /// <summary>
    /// Hoe inzetbaar dit model is voor elk van de vijf taken binnen de
    /// JabaSoft-familie (dezelfde vijf als de rolkeuzelijsten op deze kaart),
    /// op een schaal van 0 (niet geschikt) tot en met 6 (uitstekend).
    /// </summary>
    public sealed record Geschiktheid(int Chat, int Code, int Controle, int Beeld, int Zoeken, int Agent = 0);

    [GeneratedRegex(@"(?<![a-z0-9])(llava|bakllava|moondream|pixtral|qwen[\d.]*-?vl|minicpm-v|vision|cogvlm|internvl|gemma-?[34])(?![a-z0-9])")]
    private static partial Regex Beeldherkenning_();

    [GeneratedRegex(@"(?<![a-z0-9])(deepseek-r1|qwen3|qwq|o1|o3|gpt-oss|magistral|thinking|seed-oss)(?![a-z0-9])")]
    private static partial Regex Redeneermodel_();

    /// <summary>Families die bekendstaan als goed in tools gebruiken en lange agenttaken - een punt extra voor Agent.</summary>
    [GeneratedRegex(@"(?<![a-z0-9])(qwen3|gpt-oss|seed-oss|magistral|devstral|mistral-small|glm|kimi)")]
    private static partial Regex Agentfamilie_();

    /// <summary>
    /// Schat de geschiktheid per taak op basis van de grootte en de
    /// herkende soort/familie - net als <see cref="Schat"/> een indicatie,
    /// geen benchmark. Gemma3 wordt apart herkend als beeldmodel (geen
    /// algemene "vision"-term in de naam, maar wel bekend beeldherkenning).
    /// Schaal 0-5, zoals de sterren.
    ///
    /// Agent (hoofdmodel van Hermes of Goose): de grootte, een punt erbij voor
    /// een familie die goed is met tools, een punt eraf voor een klein
    /// beeldmodel (die zijn daar niet op getraind). Kan het model bekend
    /// minder dan 64.000 tokens context aan, dan hoogstens 1: daar beginnen
    /// Hermes en Goose niet eens mee.
    /// </summary>
    private static Geschiktheid SchatGeschiktheid(string laag, double? miljarden, Modelsoort soort, bool kanBeelden, bool redeneert, int maxContext)
    {
        var basis = miljarden switch
        {
            null => 3,
            < 2 => 1,
            < 5 => 2,
            < 13 => 3,
            < 25 => 4,
            _ => 5,
        };

        var beeld = kanBeelden ? 5 : 0;

        if (soort == Modelsoort.Embedding)
        {
            return new Geschiktheid(Chat: 1, Code: 0, Controle: 0, Beeld: 0, Zoeken: 5, Agent: 0);
        }

        var agent = basis
            + (Agentfamilie_().IsMatch(laag) ? 1 : 0)
            - (kanBeelden && basis <= 3 ? 1 : 0)
            - (soort == Modelsoort.Code ? 1 : 0);
        agent = Math.Clamp(agent, 1, 5);

        if (maxContext is > 0 and < Jabasoft.Base.AiBroker.HermesStatus.MinimaleContext)
        {
            agent = 1;
        }

        if (soort == Modelsoort.Code)
        {
            return new Geschiktheid(
                Chat: Math.Max(1, basis - 2),
                Code: basis,
                Controle: redeneert ? Math.Min(5, basis + 1) : basis,
                Beeld: beeld,
                Zoeken: 0,
                Agent: agent);
        }

        return new Geschiktheid(
            Chat: basis,
            Code: Math.Max(1, basis - 2),
            Controle: redeneert ? Math.Min(5, basis + 1) : Math.Max(1, basis - 1),
            Beeld: beeld,
            Zoeken: 0,
            Agent: agent);
    }
}
