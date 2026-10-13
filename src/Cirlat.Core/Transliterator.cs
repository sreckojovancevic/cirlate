using System.Text;

namespace Cirlat.Core;

/// <summary>
/// Srpska ćirilica / latinica → latinica bez dijakritika.
/// Redosled: NFC normalizacija → ćirilica u latinicu → uklanjanje dijakritika (đ → dj, џ → dž → dz).
/// Ostali ne-ASCII znakovi se NE menjaju; o njima odlučuje <see cref="NameRules"/>.
/// </summary>
public static class Transliterator
{
    // Mala slova → izlaz. Velika se izvode iz malih uz pravilo za digrafe.
    private static readonly Dictionary<char, string> Map = new()
    {
        // ćirilica, svih 30 slova
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['ђ'] = "dj", ['е'] = "e", ['ж'] = "z", ['з'] = "z", ['и'] = "i",
        ['ј'] = "j", ['к'] = "k", ['л'] = "l", ['љ'] = "lj", ['м'] = "m",
        ['н'] = "n", ['њ'] = "nj", ['о'] = "o", ['п'] = "p", ['р'] = "r",
        ['с'] = "s", ['т'] = "t", ['ћ'] = "c", ['у'] = "u", ['ф'] = "f",
        ['х'] = "h", ['ц'] = "c", ['ч'] = "c", ['џ'] = "dz", ['ш'] = "s",
        // latinica sa dijakriticima
        ['č'] = "c", ['ć'] = "c", ['š'] = "s", ['ž'] = "z", ['đ'] = "dj",
    };

    // Latinični digrafi kao jedan Unicode znak (U+01C4–U+01CC) — već nose veličinu slova.
    private static readonly Dictionary<char, string> Ligatures = new()
    {
        ['Ǆ'] = "DZ", ['ǅ'] = "Dz", ['ǆ'] = "dz",
        ['Ǉ'] = "LJ", ['ǈ'] = "Lj", ['ǉ'] = "lj",
        ['Ǌ'] = "NJ", ['ǋ'] = "Nj", ['ǌ'] = "nj",
    };

    /// <summary>Konvertuje ceo tekst (za foldere i za deo naziva fajla bez ekstenzije).</summary>
    public static string Convert(string text)
    {
        string s = text.Normalize(NormalizationForm.FormC);
        var sb = new StringBuilder(s.Length + 8);

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (Ligatures.TryGetValue(c, out var lig))
            {
                sb.Append(lig);
                continue;
            }

            char lower = char.ToLowerInvariant(c);
            if (!Map.TryGetValue(lower, out var output))
            {
                sb.Append(c);
                continue;
            }

            bool upper = c != lower;
            if (!upper)
                sb.Append(output);
            else if (output.Length == 1)
                sb.Append(char.ToUpperInvariant(output[0]));
            else
            {
                // Љубав → Ljubav, ЉУБАВ → LJUBAV, ПЕЂ → PEDJ
                bool allCaps = NextLetterIsUpper(s, i) ?? PrevLetterIsUpper(s, i);
                sb.Append(char.ToUpperInvariant(output[0]));
                sb.Append(allCaps ? char.ToUpperInvariant(output[1]) : output[1]);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Predlog naziva. Za fajl se menja samo deo pre poslednje tačke; ekstenzija ostaje u izvornom obliku
    /// (višestruke ekstenzije kao .tar.gz ostaju jer je .tar deo osnove i već je ASCII).
    /// </summary>
    public static string ConvertName(string name, bool isDirectory)
    {
        if (isDirectory) return Convert(name);
        var (stem, ext) = SplitExtension(name);
        return Convert(stem) + ext;
    }

    public static (string Stem, string Extension) SplitExtension(string name)
    {
        int dot = name.LastIndexOf('.');
        if (dot <= 0 || dot == name.Length - 1) return (name, "");
        return (name[..dot], name[dot..]);
    }

    private static bool? NextLetterIsUpper(string s, int i)
    {
        if (i + 1 < s.Length && char.IsLetter(s[i + 1]))
            return char.IsUpper(s[i + 1]);
        return null; // nema sledećeg slova
    }

    private static bool PrevLetterIsUpper(string s, int i) =>
        i > 0 && char.IsLetter(s[i - 1]) && char.IsUpper(s[i - 1]);
}
