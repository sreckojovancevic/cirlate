namespace Cirlat.Core;

public enum ItemStatus
{
    Ready,          // Spreman
    Unchanged,      // Bez promene
    Conflict,       // Konflikt naziva
    Invalid,        // Nevažeći naziv
    AccessError,    // Greška pristupa
    Done,           // Preimenovano
    Failed,         // Neuspešno
}

public readonly record struct Validation(ItemStatus Status, string Message, bool Overridable);

/// <summary>Provera predloženog naziva: strogi ASCII uslov + pravila Windows naziva.</summary>
public static class NameRules
{
    public const int MaxPath = 260;

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
    };

    private const string InvalidChars = "<>:\"/\\|?*";

    public static Validation Validate(string original, string proposed, bool isDirectory)
    {
        if (proposed == original)
        {
            char? bad = FirstNonAscii(original);
            return bad is { } b
                ? new(ItemStatus.Invalid, $"sadrži ne-ASCII znak '{b}' koji CIRLAT ne menja", false)
                : new(ItemStatus.Unchanged, "", false);
        }

        // tvrda pravila Windowsa — ne mogu se zaobići
        foreach (char c in proposed)
            if (c < 32 || InvalidChars.Contains(c))
                return new(ItemStatus.Invalid, $"nedozvoljen znak '{c}'", false);
        if (proposed.EndsWith('.') || proposed.EndsWith(' '))
            return new(ItemStatus.Invalid, "naziv se završava tačkom ili razmakom", false);
        if (proposed.Length > 255)
            return new(ItemStatus.Invalid, "naziv duži od 255 znakova", false);
        string device = proposed.Split('.')[0].TrimEnd(' ');
        if (Reserved.Contains(device))
            return new(ItemStatus.Invalid, $"rezervisan naziv u Windowsu ({device})", false);

        // strogi ASCII uslov — može se svesno prihvatiti
        if (isDirectory)
        {
            if (FirstNonAscii(proposed) is { } b)
                return new(ItemStatus.Invalid, $"ostaje ne-ASCII znak '{b}'", true);
        }
        else
        {
            var (stem, ext) = Transliterator.SplitExtension(proposed);
            if (FirstNonAscii(stem) is { } b)
                return new(ItemStatus.Invalid, $"ostaje ne-ASCII znak '{b}'", true);
            if (FirstNonAscii(ext) is not null)
                return new(ItemStatus.Invalid, "ekstenzija sadrži ne-ASCII znakove i nije menjana", true);
        }

        return new(ItemStatus.Ready, "", false);
    }

    public static char? FirstNonAscii(string s)
    {
        foreach (char c in s)
            if (c > 127) return c;
        return null;
    }

    public static string Label(ItemStatus s) => s switch
    {
        ItemStatus.Ready => "Spreman",
        ItemStatus.Unchanged => "Bez promene",
        ItemStatus.Conflict => "Konflikt naziva",
        ItemStatus.Invalid => "Nevažeći naziv",
        ItemStatus.AccessError => "Greška pristupa",
        ItemStatus.Done => "Preimenovano",
        ItemStatus.Failed => "Neuspešno",
        _ => s.ToString(),
    };
}
