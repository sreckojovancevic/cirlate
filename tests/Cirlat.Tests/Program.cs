// Jednostavan test runner bez spoljnih paketa: `dotnet run --project tests/Cirlat.Tests`
using Cirlat.Core;

int failed = 0, passed = 0;

void Eq(string actual, string expected, string label)
{
    if (actual == expected) { passed++; return; }
    failed++;
    Console.WriteLine($"FAIL {label}: očekivano \"{expected}\", dobijeno \"{actual}\"");
}

void True(bool cond, string label)
{
    if (cond) { passed++; return; }
    failed++;
    Console.WriteLine($"FAIL {label}");
}

// ── primeri iz specifikacije (2.2) ──
var spec = new (string In, string Out)[]
{
    ("Извештај.docx", "Izvestaj.docx"),
    ("Ђорђе.docx", "Djordje.docx"),
    ("Чачак.pdf", "Cacak.pdf"),
    ("Љубав.txt", "Ljubav.txt"),
    ("Његош.pdf", "Njegos.pdf"),
    ("Џемпер.jpg", "Dzemper.jpg"),
    ("Čačak_Šabac.docx", "Cacak_Sabac.docx"),
    ("Đorđe_Ćirić.pdf", "Djordje_Ciric.pdf"),
};
foreach (var (i, o) in spec) Eq(Transliterator.ConvertName(i, false), o, "spec " + i);

// ── svih 30 slova ćirilice, mala i velika ──
Eq(Transliterator.Convert("абвгдђежзијклљмнњопрстћуфхцчџш"),
   "abvgddjezzijklljmnnjoprstcufhccdzs", "30 malih");
Eq(Transliterator.Convert("АБВГДЂЕЖЗИЈКЛЉМНЊОПРСТЋУФХЦЧЏШ"),
   "ABVGDDJEZZIJKLLJMNNJOPRSTCUFHCCDZS", "30 velikih");

// ── digrafi i veličina slova ──
Eq(Transliterator.Convert("ЉУБАВ"), "LJUBAV", "LJ sve velika");
Eq(Transliterator.Convert("Љубав"), "Ljubav", "Lj naslov");
Eq(Transliterator.Convert("ЂОРЂЕ"), "DJORDJE", "DJ sve velika");
Eq(Transliterator.Convert("ЏЕМПЕР"), "DZEMPER", "DZ sve velika");
Eq(Transliterator.Convert("ПЕЂ"), "PEDJ", "DJ na kraju, sve velika");
Eq(Transliterator.Convert("Љ"), "Lj", "samo Lj");
Eq(Transliterator.Convert("ЊЊ"), "NJNJ", "NJNJ");
Eq(Transliterator.Convert("ĐORĐE"), "DJORDJE", "latinično ĐORĐE");
Eq(Transliterator.Convert("Đorđe"), "Djordje", "latinično Đorđe");
Eq(Transliterator.Convert("Ђ 2024"), "Dj 2024", "Đ pa razmak");
Eq(Transliterator.Convert("ИЗВЕШТАЈ Ђ"), "IZVESTAJ Dj", "samostalno Đ (inicijal) → Dj");
Eq(Transliterator.Convert("Ђ. Петровић"), "Dj. Petrovic", "inicijal");
Eq(Transliterator.Convert("\u01C4AK \u01C5ak \u01C6ak \u01C7 \u01C8 \u01C9 \u01CA \u01CB \u01CC"),
   "DZAK Dzak dzak LJ Lj lj NJ Nj nj", "latinične ligature");

// ── NFD (Mac) zapis ──
Eq(Transliterator.Convert("C\u030Cac\u030Cak s\u030Cabac z\u030Cuto c\u0301"), "Cacak sabac zuto c", "NFD dijakritici");

// ── mešano pismo i ostali znakovi ostaju ──
Eq(Transliterator.Convert("Сacak 2024 (копија) - final"), "Sacak 2024 (kopija) - final", "mešano");
Eq(Transliterator.Convert("café"), "café", "é se ne dira");

// ── ekstenzije ──
Eq(Transliterator.ConvertName("Архива.tar.gz", false), "Arhiva.tar.gz", "višestruka ekstenzija");
Eq(Transliterator.ConvertName(".gitignore", false), ".gitignore", "dotfile");
Eq(Transliterator.ConvertName("Документ.ДОК", false), "Dokument.ДОК", "ćirilična ekstenzija ostaje");
Eq(Transliterator.ConvertName("Фасцикла.v2", true), "Fascikla.v2", "folder: ceo naziv");
Eq(Transliterator.ConvertName("Шема.", false), "Sema.", "tačka na kraju");

// ── provera naziva ──
True(NameRules.Validate("Čačak.pdf", "Cacak.pdf", false).Status == ItemStatus.Ready, "ready");
True(NameRules.Validate("Cacak.pdf", "Cacak.pdf", false).Status == ItemStatus.Unchanged, "unchanged");
var v1 = NameRules.Validate("Čačak café.pdf", "Cacak café.pdf", false);
True(v1.Status == ItemStatus.Invalid && v1.Overridable, "ne-ASCII ostaje → nevažeći, može se prihvatiti");
var v2 = NameRules.Validate("café.pdf", "café.pdf", false);
True(v2.Status == ItemStatus.Invalid && !v2.Overridable, "samo ne-ASCII bez promene → prijava");
var v3 = NameRules.Validate("Документ.ДОК", "Dokument.ДОК", false);
True(v3.Status == ItemStatus.Invalid && v3.Overridable, "ćirilična ekstenzija → upozorenje");
var v4 = NameRules.Validate("Цон.txt", "Con.txt", false);
True(v4.Status == ItemStatus.Invalid && !v4.Overridable, "CON rezervisan");
var v5 = NameRules.Validate("Лпт1", "Lpt1", true);
True(v5.Status == ItemStatus.Invalid && !v5.Overridable, "LPT1 rezervisan");

// ── konflikti (bez obzira na veličinu slova) ──
RenameItem Mk(string dir, string name)
{
    var it = new RenameItem { FullPath = dir + "\\" + name, ParentDir = dir, Name = name, IsDirectory = false, Depth = 2 };
    it.Proposed = Transliterator.ConvertName(name, false);
    it.BaseProposed = it.Proposed;
    it.Base = NameRules.Validate(it.Name, it.Proposed, false);
    it.Include = true;
    it.Status = it.Base.Status;
    return it;
}

{
    var a = Mk(@"C:\x", "Šabac.docx");
    var sib = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    { [@"C:\x"] = ["Šabac.docx", "sabac.DOCX", "Ostalo.txt"] };
    RenamePlan.MarkConflicts(sib, [a]);
    True(a.Status == ItemStatus.Conflict, "konflikt sa postojećim (case-insensitive)");
}
{
    var a = Mk(@"C:\x", "Čačak.pdf");
    var b = Mk(@"C:\x", "Cačak.pdf");
    var sib = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    { [@"C:\x"] = ["Čačak.pdf", "Cačak.pdf"] };
    RenamePlan.MarkConflicts(sib, [a, b]);
    True(a.Status == ItemStatus.Conflict && b.Status == ItemStatus.Conflict, "dve stavke dobijaju isti naziv");
}
{
    var a = Mk(@"C:\x", "Čačak.pdf");
    var b = Mk(@"C:\y", "Čačak.pdf");
    var sib = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    { [@"C:\x"] = ["Čačak.pdf"], [@"C:\y"] = ["Čačak.pdf"] };
    RenamePlan.MarkConflicts(sib, [a, b]);
    True(a.Status == ItemStatus.Ready && b.Status == ItemStatus.Ready, "isti naziv u različitim folderima nije konflikt");
}

// ── sufiks umesto konflikta (opcija) ──
{
    var a = Mk(@"C:\x", "Šabac.docx");
    var sib = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    { [@"C:\x"] = ["Šabac.docx", "sabac.docx", "Sabac (2).docx"] };
    RenamePlan.ResolveWithSuffix(sib, [a]);
    Eq(a.Proposed, "Sabac (3).docx", "sufiks preskače zauzet (2)");
    True(a.Status == ItemStatus.Ready, "sufiks: status ostaje Spreman");
}
{
    var a = Mk(@"C:\x", "Čačak.pdf");
    var b = Mk(@"C:\x", "Cačak.pdf");
    var sib = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    { [@"C:\x"] = ["Čačak.pdf", "Cačak.pdf"] };
    RenamePlan.ResolveWithSuffix(sib, [a, b]);
    Eq(b.Proposed, "Cacak.pdf", "prva po abecedi dobija osnovni naziv");
    Eq(a.Proposed, "Cacak (2).pdf", "druga dobija (2)");
}
{
    var d = new RenameItem { FullPath = @"C:\x\Ђорђе", ParentDir = @"C:\x", Name = "Ђорђе", IsDirectory = true, Depth = 2 };
    d.Proposed = d.BaseProposed = Transliterator.ConvertName(d.Name, true);
    var sib = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    { [@"C:\x"] = ["Ђорђе", "Djordje"] };
    RenamePlan.ResolveWithSuffix(sib, [d]);
    Eq(d.Proposed, "Djordje (2)", "folder dobija sufiks na kraju naziva");
}
{
    var a = Mk(@"C:\x", "Архива.tar.gz");
    var sib = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    { [@"C:\x"] = ["Архива.tar.gz", "Arhiva.tar.gz"] };
    RenamePlan.ResolveWithSuffix(sib, [a]);
    Eq(a.Proposed, "Arhiva (2).tar.gz", "višestruka ekstenzija: sufiks ispred .tar.gz");
}

// ── putanje ──
True(PathResolver.IsSameOrUnder(@"C:\Program Files\X", @"C:\Program Files"), "ispod");
True(!PathResolver.IsSameOrUnder(@"C:\Program Files (x86)\X", @"C:\Program Files"), "sličan prefiks nije ispod");
True(PathResolver.IsSameOrUnder(@"c:\users\a\documents", @"C:\Users\A"), "case-insensitive");
True(PathResolver.IsStrictlyUnder(@"C:\Users\A\Documents", @"C:\Users\A"), "strogo ispod");
True(!PathResolver.IsStrictlyUnder(@"C:\Users\A", @"C:\Users\A"), "jednako nije strogo ispod");

Console.WriteLine($"{passed} prošlo, {failed} palo");
return failed == 0 ? 0 : 1;
