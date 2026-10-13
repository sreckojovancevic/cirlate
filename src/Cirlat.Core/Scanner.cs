namespace Cirlat.Core;

public enum ItemKind { FilesAndFolders, FilesOnly, FoldersOnly }

public sealed class ScanOptions
{
    public List<string> Roots { get; init; } = [];
    public bool Recursive { get; init; } = true;
    public ItemKind Kind { get; init; } = ItemKind.FilesAndFolders;
}

public sealed class ScanResult
{
    public List<RenameItem> Items { get; } = [];
    /// <summary>Sva imena u svakom obiđenom folderu (i ona koja se ne obrađuju) — za proveru konflikata.</summary>
    public Dictionary<string, List<string>> Siblings { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Recursive { get; set; }
    public int SkippedProtected { get; set; }
    public int SkippedSystemHidden { get; set; }
    public int SkippedLinks { get; set; }
}

public static class Scanner
{
    private static readonly EnumerationOptions Enum = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
        MatchType = MatchType.Simple,
    };

    public static ScanResult Scan(ScanOptions opt, ProtectedPaths prot, IProgress<int>? progress, CancellationToken ct)
    {
        var result = new ScanResult { Recursive = opt.Recursive };
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var itemPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allLengths = new List<(string Path, int Len)>();
        int seen = 0;

        // (folder, da li ispod treba proveravati zaštitu)
        var stack = new Stack<(string Dir, bool Check)>();
        foreach (var r in opt.Roots)
        {
            string root = PathResolver.Canonical(r);
            if (prot.Classify(root).Kind == ProtectionKind.Blocked) { result.SkippedProtected++; continue; }
            stack.Push((root, prot.NeedsCheckBelow(root)));
        }

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (dir, check) = stack.Pop();
            if (!visited.Add(dir)) continue;

            List<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(dir).EnumerateFileSystemInfos("*", Enum).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                result.Items.Add(new RenameItem
                {
                    FullPath = dir, ParentDir = Path.GetDirectoryName(dir) ?? dir, Name = Path.GetFileName(dir),
                    IsDirectory = true, Depth = Depth(dir), IsListingError = true, Proposed = Path.GetFileName(dir),
                    Base = new(ItemStatus.AccessError, "sadržaj foldera nije dostupan: " + ex.Message, false),
                    Status = ItemStatus.AccessError, Message = "sadržaj foldera nije dostupan: " + ex.Message,
                });
                continue;
            }

            result.Siblings[dir] = entries.Select(e => e.Name).ToList();

            foreach (var e in entries)
            {
                if (++seen % 500 == 0) progress?.Report(seen);
                var attr = e.Attributes;
                bool isDir = (attr & FileAttributes.Directory) != 0;

                if ((attr & FileAttributes.ReparsePoint) != 0) { result.SkippedLinks++; continue; }
                if ((attr & FileAttributes.Hidden) != 0 && (attr & FileAttributes.System) != 0) { result.SkippedSystemHidden++; continue; }

                string path = e.FullName;
                var kind = ProtectionKind.Allowed;
                bool checkBelow = false;
                if (check)
                {
                    kind = prot.Classify(path).Kind;
                    if (kind == ProtectionKind.Blocked) { result.SkippedProtected++; continue; }
                    checkBelow = isDir && prot.NeedsCheckBelow(path);
                }

                allLengths.Add((path, path.Length));

                bool wanted = opt.Kind switch
                {
                    ItemKind.FilesOnly => !isDir,
                    ItemKind.FoldersOnly => isDir,
                    _ => true,
                };

                if (wanted && kind == ProtectionKind.Allowed && itemPaths.Add(path))
                {
                    var item = new RenameItem
                    {
                        FullPath = path, ParentDir = dir, Name = e.Name, IsDirectory = isDir, Depth = Depth(path),
                        Proposed = Transliterator.ConvertName(e.Name, isDir),
                    };
                    item.Base = NameRules.Validate(item.Name, item.Proposed, isDir);
                    item.Include = item.Base.Status == ItemStatus.Ready;
                    result.Items.Add(item);
                }

                if (isDir && opt.Recursive)
                    stack.Push((path, checkBelow));
            }
        }

        // najduža putanja ispod svakog foldera (samo kad je obilazak rekurzivan)
        if (opt.Recursive)
        {
            var max = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (p, len) in allLengths)
            {
                string? parent = Path.GetDirectoryName(p);
                while (parent != null && visited.Contains(parent))
                {
                    if (max.TryGetValue(parent, out int m) && m >= len) break;
                    max[parent] = len;
                    parent = Path.GetDirectoryName(parent);
                }
            }
            foreach (var it in result.Items.Where(i => i.IsDirectory && !i.IsListingError))
                it.MaxDescendantPathLength = max.TryGetValue(it.FullPath, out int m) ? m : it.FullPath.Length;
        }

        progress?.Report(seen);
        return result;
    }

    /// <summary>Za nerekurzivni obilazak: izmeri sadržaj foldera koji bi se produžio.</summary>
    public static int MeasureDescendants(string dir)
    {
        int max = dir.Length;
        try
        {
            var o = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var p in Directory.EnumerateFileSystemEntries(dir, "*", o))
                if (p.Length > max) max = p.Length;
        }
        catch { }
        return max;
    }

    private static int Depth(string p) => p.Count(c => c == '\\' || c == '/');
}
