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
    private Modelkeuze(string naam, double? miljardenParameters, string? kwantisatie, Modelsoort soort, int sterren)
    {
        Naam = naam;
        MiljardenParameters = miljardenParameters;
        Kwantisatie = kwantisatie;
        Soort = soort;
        Sterren = sterren;
    }

    public string Naam { get; }

    /// <summary>Het aantal parameters in miljarden, als de naam dat noemt (14b, 1.5b).</summary>
    public double? MiljardenParameters { get; }

    /// <summary>Bijvoorbeeld "Q4_K_M", als de naam dat noemt.</summary>
    public string? Kwantisatie { get; }

    public Modelsoort Soort { get; }

    /// <summary>0 tot en met 5.</summary>
    public int Sterren { get; }

    /// <summary>De sterren als tekst, bijvoorbeeld ★★★★☆.</summary>
    public string SterrenTekst => new string('★', Sterren) + new string('☆', 5 - Sterren);

    /// <summary>De grootte als tekst ("14B"), of leeg als de naam er niets over zegt.</summary>
    public string Grootte => MiljardenParameters is { } miljarden
        ? miljarden.ToString("0.#", CultureInfo.InvariantCulture) + "B"
        : string.Empty;

    /// <summary>De regel achter de naam: grootte, soort en eventueel kwantisatie.</summary>
    public string Info { get; private set; } = string.Empty;

    public override string ToString() => Naam;

    /// <summary>
    /// Maakt de keuze voor een naam. <paramref name="soortNaam"/> vertaalt
    /// een soort naar tekst in de taal van nu.
    /// </summary>
    public static Modelkeuze Van(string naam, Func<Modelsoort, string> soortNaam)
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

        var keuze = new Modelkeuze(naam, miljarden, kwantisatie, soort, Schat(miljarden, kwantisatie, soort));

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
}
