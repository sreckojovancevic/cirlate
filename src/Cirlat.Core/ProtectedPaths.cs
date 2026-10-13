using Microsoft.Win32;

namespace Cirlat.Core;

public enum ProtectionKind
{
    /// <summary>Može se obrađivati i preimenovati.</summary>
    Allowed,
    /// <summary>Koren dozvoljenog korisničkog foldera (Documents, Desktop, OneDrive…): sadržaj da, sam folder ne.</summary>
    NoRename,
    /// <summary>Zaštićeno: ne bira se, ne obilazi se, ništa se ne preimenuje — ni ono ispod.</summary>
    Blocked,
}

public readonly record struct Protection(ProtectionKind Kind, string Reason);

/// <summary>
/// Zaštićene lokacije. Ništa nije zakucano: sistemski disk, Program Files, profili i
/// korisnički folderi se pitaju od Windowsa, pa radi i kad sistem nije na C:.
///
/// Tri sloja:
///  1. stvarne lokacije sistema (Known Folders, ProfileList, promenljive okruženja)
///  2. ista sistemska imena u korenu SVAKOG diska / share-a (dual boot, stari sistemski disk, D:\Program Files)
///  3. folderi instaliranih programa (InstallLocation iz Uninstall ključeva), gde god da su
/// U profilu trenutnog korisnika dozvoljen je samo sadržaj Desktop/Documents/Downloads/
/// Pictures/Music/Videos i OneDrive/SharePoint sync foldera. Ostatak profila (AppData…) je zaštićen.
/// </summary>
public sealed class ProtectedPaths
{
    /// <summary>Imena koja se štite u korenu svakog diska ili share-a.</summary>
    public static readonly HashSet<string> RootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Windows.old", "Program Files", "Program Files (x86)", "ProgramData",
        "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs", "Boot",
        "$WinREAgent", "Config.Msi", "$Windows.~BT", "$Windows.~WS", "$SysReset",
        "Documents and Settings", "MSOCache", "inetpub", "OneDriveTemp",
    };

    /// <summary>Koren profila na svakom disku tretira se kao profil (zaštićen osim dozvoljenih foldera).</summary>
    public const string UsersRootName = "Users";

    public List<string> SystemTrees { get; } = [];
    public List<string> ProfileTrees { get; } = [];
    public List<string> InstallTrees { get; } = [];
    public List<string> AllowedRoots { get; } = [];
    public string? CurrentProfile { get; private set; }

    private List<string> _all = [];

    public static ProtectedPaths Build()
    {
        var pp = new ProtectedPaths();
        pp.Collect();
        pp._all = pp.SystemTrees.Concat(pp.ProfileTrees).Concat(pp.InstallTrees).Concat(pp.AllowedRoots).ToList();
        return pp;
    }

    // ───────────────────────────── klasifikacija ─────────────────────────────

    /// <summary>Putanja mora biti kanonska (<see cref="PathResolver.Canonical"/>).</summary>
    public Protection Classify(string path)
    {
        path = PathResolver.Trim(path);
        string root = PathResolver.Trim(Path.GetPathRoot(path) ?? "");
        if (root.Length == 0 || PathResolver.Equal(path, root))
            return new(ProtectionKind.Blocked, "koren diska");

        string first = path[root.Length..].TrimStart('\\').Split('\\')[0];

        if (InstallTrees.Any(t => PathResolver.IsSameOrUnder(path, t)))
            return new(ProtectionKind.Blocked, "folder instaliranog programa");

        if (RootNames.Contains(first) || SystemTrees.Any(t => PathResolver.IsSameOrUnder(path, t)))
            return new(ProtectionKind.Blocked, "sistemska lokacija");

        if (AllowedRoots.Any(a => PathResolver.Equal(path, a)))
            return new(ProtectionKind.NoRename, "korisnički folder (sadržaj se može obrađivati)");
        if (AllowedRoots.Any(a => PathResolver.IsStrictlyUnder(path, a)))
            return new(ProtectionKind.Allowed, "");

        if (first.Equals(UsersRootName, StringComparison.OrdinalIgnoreCase) ||
            ProfileTrees.Any(t => PathResolver.IsSameOrUnder(path, t)))
            return new(ProtectionKind.Blocked, "korisnički profil / sistemski deo profila");

        return new(ProtectionKind.Allowed, "");
    }

    /// <summary>
    /// Da li ispod foldera može postojati nešto što se drugačije klasifikuje.
    /// Ako ne može, sav sadržaj nasleđuje klasifikaciju foldera (brži obilazak).
    /// </summary>
    public bool NeedsCheckBelow(string dir)
    {
        dir = PathResolver.Trim(dir);
        string root = PathResolver.Trim(Path.GetPathRoot(dir) ?? "");
        if (PathResolver.Equal(dir, root)) return true;
        return _all.Any(t => PathResolver.IsStrictlyUnder(t, dir));
    }

    // ───────────────────────────── prikupljanje ─────────────────────────────

    private void Collect()
    {
        // 1. sistem
        foreach (var f in new[]
                 {
                     Environment.SpecialFolder.Windows, Environment.SpecialFolder.System,
                     Environment.SpecialFolder.SystemX86, Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonProgramFiles,
                     Environment.SpecialFolder.CommonProgramFilesX86, Environment.SpecialFolder.CommonApplicationData,
                 })
            Add(SystemTrees, Environment.GetFolderPath(f));
        foreach (var v in new[] { "SystemRoot", "windir", "ProgramFiles", "ProgramW6432", "ProgramFiles(x86)", "ProgramData" })
            Add(SystemTrees, Environment.GetEnvironmentVariable(v));

        // profili: folder sa profilima (može biti D:\Users), Default, Public, svaki profil na mašini
        CurrentProfile = Canon(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add(ProfileTrees, CurrentProfile);
        try
        {
            using var pl = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
            if (pl != null)
            {
                foreach (var name in new[] { "ProfilesDirectory", "Default", "Public" })
                    Add(ProfileTrees, Expand(pl.GetValue(name) as string));
                foreach (var sid in pl.GetSubKeyNames())
                {
                    using var k = pl.OpenSubKey(sid);
                    Add(ProfileTrees, Expand(k?.GetValue("ProfileImagePath") as string));
                }
            }
        }
        catch { /* bez pristupa: ostaju slojevi 2 i 3 i trenutni profil */ }

        // dozvoljeni korisnički folderi trenutnog korisnika
        foreach (var f in new[]
                 {
                     Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic,
                     Environment.SpecialFolder.MyVideos,
                 })
            AddAllowed(Environment.GetFolderPath(f));
        AddAllowed(Native.KnownFolder(Native.FolderDownloads));
        foreach (var v in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" })
            AddAllowed(Environment.GetEnvironmentVariable(v));
        try
        {
            using var acc = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts");
            foreach (var n in acc?.GetSubKeyNames() ?? [])
            {
                using var k = acc!.OpenSubKey(n);
                AddAllowed(k?.GetValue("UserFolder") as string);
            }
        }
        catch { }
        try
        {
            // SharePoint biblioteke sinhronizovane kroz OneDrive
            using var prov = Registry.CurrentUser.OpenSubKey(@"Software\SyncEngines\Providers\OneDrive");
            foreach (var n in prov?.GetSubKeyNames() ?? [])
            {
                using var k = prov!.OpenSubKey(n);
                AddAllowed(k?.GetValue("MountPoint") as string);
            }
        }
        catch { }

        // 3. instalirani programi
        foreach (var (hive, view) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser, RegistryView.Default),
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var un = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                foreach (var n in un?.GetSubKeyNames() ?? [])
                {
                    using var k = un!.OpenSubKey(n);
                    AddInstall(Expand(k?.GetValue("InstallLocation") as string));
                }
            }
            catch { }
        }
    }

    private static string? Expand(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : Environment.ExpandEnvironmentVariables(s.Trim().Trim('"'));

    private static string? Canon(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try { return PathResolver.Canonical(s); } catch { return null; }
    }

    private static void Add(List<string> list, string? raw)
    {
        var c = Canon(raw);
        if (c == null || PathResolver.Equal(c, Path.GetPathRoot(c) ?? "")) return;
        if (!list.Any(x => PathResolver.Equal(x, c))) list.Add(c);
    }

    private void AddAllowed(string? raw)
    {
        var c = Canon(raw);
        if (c == null || !Directory.Exists(c)) return;
        if (CurrentProfile != null && PathResolver.Equal(c, CurrentProfile)) return;       // ceo profil nikad
        if (SystemTrees.Any(t => PathResolver.IsSameOrUnder(c, t))) return;              // preusmeren u sistem? ne.
        Add(AllowedRoots, c);
    }

    private void AddInstall(string? raw)
    {
        var c = Canon(raw);
        if (c == null || !Directory.Exists(c)) return;
        if (PathResolver.Equal(c, Path.GetPathRoot(c) ?? "")) return;
        // pokvaren unos (npr. InstallLocation = Documents ili profil) ne sme da zaključa korisničke foldere
        if (AllowedRoots.Any(a => PathResolver.IsSameOrUnder(a, c))) return;
        if (ProfileTrees.Any(p => PathResolver.Equal(p, c))) return;
        Add(InstallTrees, c);
    }
}
