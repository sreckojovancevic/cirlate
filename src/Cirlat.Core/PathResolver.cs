namespace Cirlat.Core;

/// <summary>
/// Svodi putanju na stvarnu: puna putanja, duga imena umesto 8.3,
/// SUBST disk (npr. Short) → stvarni folder, mrežni disk → UNC.
/// Zaštita se uvek proverava nad stvarnom putanjom.
/// </summary>
public static class PathResolver
{
    public static string Canonical(string path)
    {
        string p = Strip(Path.GetFullPath(path.Trim().Trim('"')));

        for (int depth = 0; depth < 16; depth++)
        {
            if (p.Length < 2 || p[1] != ':' || !char.IsAsciiLetter(p[0])) break;
            string dev = char.ToUpperInvariant(p[0]) + ":";
            string rest = p.Length > 2 ? p[2..].TrimStart('\\') : "";

            string? target = null;
            string? dos = SafeQueryDos(dev);
            if (dos != null && dos.StartsWith(@"\??\", StringComparison.Ordinal))
            {
                target = dos.StartsWith(@"\??\UNC\", StringComparison.OrdinalIgnoreCase)
                    ? @"\\" + dos[8..]
                    : dos[4..];
            }
            else
            {
                target = SafeNetwork(dev);
            }

            if (target == null) break; // pravi lokalni disk
            p = rest.Length == 0 ? target : target.TrimEnd('\\') + "\\" + rest;
        }

        string? lng = SafeLong(p);
        if (lng != null) p = Strip(lng);

        return Trim(p);
    }

    public static string Trim(string p)
    {
        string root = Path.GetPathRoot(p) ?? "";
        if (p.Length > root.Length) p = p.TrimEnd('\\');
        return p;
    }

    public static bool Equal(string a, string b) =>
        string.Equals(Trim(a), Trim(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>path je jednak ancestor-u ili se nalazi ispod njega.</summary>
    public static bool IsSameOrUnder(string path, string ancestor)
    {
        path = Trim(path);
        ancestor = Trim(ancestor);
        if (path.Length == ancestor.Length) return path.Equals(ancestor, StringComparison.OrdinalIgnoreCase);
        if (path.Length < ancestor.Length) return false;
        string prefix = ancestor.EndsWith('\\') ? ancestor : ancestor + "\\";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStrictlyUnder(string path, string ancestor) =>
        !Equal(path, ancestor) && IsSameOrUnder(path, ancestor);

    private static string Strip(string p)
    {
        if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + p[8..];
        if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) return p[4..];
        return p;
    }

    private static string? SafeQueryDos(string dev) { try { return Native.QueryDosDeviceFirst(dev); } catch { return null; } }
    private static string? SafeNetwork(string dev) { try { return Native.NetworkTarget(dev); } catch { return null; } }
    private static string? SafeLong(string p) { try { return Native.LongName(p); } catch { return null; } }
}
