namespace Cirlat.Core;

public sealed record UndoResult(int Restored, List<string> Failures);

public static class Executor
{
    /// <summary>
    /// Izvršava plan. Redosled: najdublje stavke prvo (folderi odozdo nagore), pa putanje
    /// do sadržaja ostaju važeće dok se ne preimenuje roditelj.
    /// Nikad ne prepisuje: pre svakog poteza proverava da cilj ne postoji, a Move sam odbija postojeći cilj.
    /// </summary>
    public static Journal? Execute(RenamePlan plan, IProgress<(int Done, int Total)>? progress, CancellationToken ct)
    {
        var work = plan.WillRename
            .OrderByDescending(i => i.Depth)
            .ThenBy(i => i.IsDirectory) // fajlovi pre foldera na istoj dubini
            .ToList();
        if (work.Count == 0) return null;

        var journal = Journal.Begin();
        int done = 0;

        foreach (var it in work)
        {
            if (ct.IsCancellationRequested) break;

            string src = it.FullPath;
            string dst = it.ProposedFullPath;
            try
            {
                bool srcExists = it.IsDirectory ? Directory.Exists(src) : File.Exists(src);
                if (!srcExists)
                    Fail(it, "stavka više ne postoji na tom mestu");
                else if (File.Exists(dst) || Directory.Exists(dst))
                    Fail(it, "ciljni naziv u međuvremenu već postoji — nije prepisano");
                else
                {
                    if (it.IsDirectory) Directory.Move(src, dst);
                    else File.Move(src, dst, overwrite: false);

                    journal.Append(new JournalEntry(src, dst, it.IsDirectory, DateTimeOffset.Now));
                    it.Status = ItemStatus.Done;
                    it.Message = "";
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Fail(it, ex.Message);
            }

            progress?.Report((++done, work.Count));
        }

        return journal;
    }

    /// <summary>Vraća originalne nazive obrnutim redosledom (folderi odozgo nadole).</summary>
    public static UndoResult Undo(Journal journal, IProgress<(int Done, int Total)>? progress, CancellationToken ct)
    {
        var entries = journal.Read();
        var remaining = new List<JournalEntry>();
        var failures = new List<string>();
        int restored = 0, n = 0;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (ct.IsCancellationRequested) { remaining.Insert(0, e); continue; }
            try
            {
                bool exists = e.Dir ? Directory.Exists(e.New) : File.Exists(e.New);
                if (!exists)
                {
                    failures.Add($"{e.New}: ne postoji (premešteno ili preimenovano u međuvremenu)");
                    remaining.Insert(0, e);
                }
                else if (File.Exists(e.Old) || Directory.Exists(e.Old))
                {
                    failures.Add($"{e.Old}: originalni naziv je u međuvremenu zauzet");
                    remaining.Insert(0, e);
                }
                else
                {
                    if (e.Dir) Directory.Move(e.New, e.Old);
                    else File.Move(e.New, e.Old, overwrite: false);
                    restored++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{e.New}: {ex.Message}");
                remaining.Insert(0, e);
            }
            progress?.Report((++n, entries.Count));
        }

        journal.Finish(remaining);
        return new UndoResult(restored, failures);
    }

    private static void Fail(RenameItem it, string msg)
    {
        it.Status = ItemStatus.Failed;
        it.Message = msg;
    }
}
