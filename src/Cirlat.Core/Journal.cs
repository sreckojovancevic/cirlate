using System.Text.Json;

namespace Cirlat.Core;

public sealed record JournalEntry(string Old, string New, bool Dir, DateTimeOffset Time);

/// <summary>
/// Trajni dnevnik operacija: %LOCALAPPDATA%\Cirlat\journal\yyyyMMdd-HHmmss.jsonl
/// Svaki uspešan rename se upisuje odmah (jedna linija), pa poništavanje radi i posle pada ili restarta.
/// </summary>
public sealed class Journal
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cirlat", "journal");

    public string FilePath { get; }

    private Journal(string path) => FilePath = path;

    public static Journal Begin()
    {
        Directory.CreateDirectory(Dir);
        string name = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(Dir, name + ".jsonl");
        for (int i = 2; File.Exists(path); i++)
            path = Path.Combine(Dir, $"{name}-{i}.jsonl");
        File.WriteAllText(path, "");
        return new Journal(path);
    }

    public void Append(JournalEntry e) =>
        File.AppendAllText(FilePath, JsonSerializer.Serialize(e, Json) + Environment.NewLine);

    public List<JournalEntry> Read()
    {
        var list = new List<JournalEntry>();
        foreach (var line in File.ReadAllLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var e = JsonSerializer.Deserialize<JournalEntry>(line, Json);
                if (e != null) list.Add(e);
            }
            catch { /* oštećena poslednja linija posle pada — preskače se */ }
        }
        return list;
    }

    /// <summary>Poslednja operacija koja još nije poništena.</summary>
    public static Journal? LatestUndoable()
    {
        if (!Directory.Exists(Dir)) return null;
        var file = new DirectoryInfo(Dir).GetFiles("*.jsonl")
            .Where(f => !f.Name.EndsWith(".undone.jsonl", StringComparison.OrdinalIgnoreCase) && f.Length > 0)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        return file == null ? null : new Journal(file.FullName);
    }

    /// <summary>Posle poništavanja: ostaju samo stavke koje nisu vraćene (za ponovni pokušaj).</summary>
    public void Finish(List<JournalEntry> remaining)
    {
        if (remaining.Count == 0)
        {
            string done = Path.ChangeExtension(FilePath, null) + ".undone.jsonl";
            File.Move(FilePath, done, overwrite: true);
        }
        else
        {
            File.WriteAllLines(FilePath, remaining.Select(e => JsonSerializer.Serialize(e, Json)));
        }
    }
}
