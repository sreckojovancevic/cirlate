namespace Cirlat.Core;

/// <summary>
/// Plan preimenovanja: konačni statusi posle provere konflikata i dužine putanje.
/// Recompute se poziva posle analize i posle svake promene kvačice.
/// </summary>
public sealed class RenamePlan
{
    public ScanResult Scan { get; }
    public List<RenameItem> Items => Scan.Items;

    /// <summary>Opcija: konflikt se rešava sufiksom " (2)", " (3)"… umesto da stavka ostane nepreimenovana.</summary>
    public bool SuffixConflicts { get; set; }

    public RenamePlan(ScanResult scan, bool suffixConflicts = false)
    {
        Scan = scan;
        SuffixConflicts = suffixConflicts;
        foreach (var it in Items) it.BaseProposed = it.Proposed;
        if (!scan.Recursive)
            foreach (var it in Items.Where(i => i.IsDirectory && !i.IsListingError && i.Proposed.Length > i.Name.Length))
                it.MaxDescendantPathLength = Scanner.MeasureDescendants(it.FullPath);
        Recompute();

        // upozorenja (npr. 260 znakova) podrazumevano nisu uključena; korisnik ih može svesno uključiti
        bool changed = false;
        foreach (var it in Items.Where(i => i.Include && i.Status == ItemStatus.Invalid))
        {
            it.Include = false;
            changed = true;
        }
        if (changed) Recompute();
    }

    public IEnumerable<RenameItem> WillRename => Items.Where(i => i.Include && i.CanRename && i.Status != ItemStatus.Conflict);

    public void Recompute()
    {
        foreach (var it in Items.Where(i => !i.IsListingError))
        {
            it.Proposed = it.BaseProposed;
            it.Status = it.Base.Status;
            it.Message = it.Base.Message;
        }

        var active = Items.Where(i => i.Include && i.CanRename).ToList();
        if (SuffixConflicts) ResolveWithSuffix(Scan.Siblings, active);
        else MarkConflicts(Scan.Siblings, active);
        var candidates = Items.Where(i => i.CanRename && i.Status != ItemStatus.Conflict).ToList();
        MarkPathLength(candidates, active.Where(i => i.Status != ItemStatus.Conflict).ToList());
    }

    /// <summary>
    /// U svakom folderu skup konačnih naziva (preimenovani + svi ostali koji ostaju)
    /// ne sme da ima duplikat bez obzira na velika/mala slova.
    /// </summary>
    public static void MarkConflicts(Dictionary<string, List<string>> siblings, List<RenameItem> active)
    {
        foreach (var group in active.GroupBy(i => i.ParentDir, StringComparer.OrdinalIgnoreCase))
        {
            var renamed = group.ToDictionary(i => i.Name, StringComparer.Ordinal);
            var names = siblings.TryGetValue(group.Key, out var s) ? s : group.Select(i => i.Name).ToList();

            var finals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in names)
            {
                string f = renamed.TryGetValue(n, out var it) ? it.Proposed : n;
                finals[f] = finals.GetValueOrDefault(f) + 1;
            }

            foreach (var it in group)
            {
                if (finals.GetValueOrDefault(it.Proposed) > 1)
                {
                    it.Status = ItemStatus.Conflict;
                    it.Message = $"naziv \"{it.Proposed}\" već postoji ili ga dobija druga stavka";
                }
            }
        }
    }

    /// <summary>
    /// Umesto konflikta dodeljuje prvi slobodan naziv "Naziv (2).ext", "Naziv (3).ext"…
    /// Postojeće stavke koje se ne preimenuju zadržavaju svoj naziv; među stavkama koje se
    /// preimenuju, osnovni naziv dobija prva po abecedi originalnog naziva, ostale sufiks.
    /// </summary>
    public static void ResolveWithSuffix(Dictionary<string, List<string>> siblings, List<RenameItem> active)
    {
        foreach (var group in active.GroupBy(i => i.ParentDir, StringComparer.OrdinalIgnoreCase))
        {
            var renamedNames = new HashSet<string>(group.Select(i => i.Name), StringComparer.Ordinal);
            var names = siblings.TryGetValue(group.Key, out var s) ? s : [];

            // nazivi koji ostaju (ne preimenuju se) su zauzeti
            var taken = new HashSet<string>(names.Where(n => !renamedNames.Contains(n)), StringComparer.OrdinalIgnoreCase);

            foreach (var it in group.OrderBy(i => i.Name, StringComparer.Ordinal))
            {
                if (taken.Add(it.Proposed)) continue;

                var (stem, ext) = it.IsDirectory ? (it.Proposed, "") : SplitForSuffix(it.Proposed);
                for (int n = 2; ; n++)
                {
                    string candidate = $"{stem} ({n}){ext}";
                    if (!taken.Add(candidate)) continue;
                    it.Proposed = candidate;
                    it.Message = $"naziv \"{it.BaseProposed}\" je zauzet — dodat sufiks ({n})";
                    break;
                }
            }
        }
    }

    private static readonly string[] CompoundExtensions = [".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst"];

    private static (string Stem, string Ext) SplitForSuffix(string name)
    {
        foreach (var ce in CompoundExtensions)
            if (name.Length > ce.Length && name.EndsWith(ce, StringComparison.OrdinalIgnoreCase))
                return (name[..^ce.Length], name[^ce.Length..]);
        return Transliterator.SplitExtension(name);
    }

    /// <summary>Upozorenje ako bi preimenovanje (đ→dj, љ→lj, џ→dz) nešto gurnulo preko 260 znakova.</summary>
    private static void MarkPathLength(List<RenameItem> candidates, List<RenameItem> active)
    {
        var delta = active.ToDictionary(i => i.FullPath, i => i.Proposed.Length - i.Name.Length, StringComparer.OrdinalIgnoreCase);

        int Growth(RenameItem it)
        {
            // sopstvena promena se računa uvek (i kad stavka nije uključena), a preci samo ako su uključeni
            int g = it.Proposed.Length - it.Name.Length;
            string? p = Path.GetDirectoryName(it.FullPath);
            while (p != null)
            {
                if (delta.TryGetValue(p, out int d)) g += d;
                p = Path.GetDirectoryName(p);
            }
            return g;
        }

        foreach (var it in candidates)
        {
            int g = Growth(it);
            if (g <= 0) continue;

            int before = it.IsDirectory && it.MaxDescendantPathLength > 0
                ? Math.Max(it.FullPath.Length, it.MaxDescendantPathLength)
                : it.FullPath.Length;

            if (before < NameRules.MaxPath && before + g >= NameRules.MaxPath)
            {
                it.Status = ItemStatus.Invalid;
                it.Message = it.IsDirectory
                    ? $"posle preimenovanja putanja u ovom folderu prelazi {NameRules.MaxPath} znakova"
                    : $"posle preimenovanja putanja prelazi {NameRules.MaxPath} znakova";
            }
        }
    }

    public Dictionary<ItemStatus, int> Counts() =>
        Items.GroupBy(i => i.Status).ToDictionary(g => g.Key, g => g.Count());
}
