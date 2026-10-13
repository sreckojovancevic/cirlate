namespace Cirlat.Core;

public sealed class RenameItem
{
    public required string FullPath { get; init; }
    public required string ParentDir { get; init; }
    public required string Name { get; init; }
    public required bool IsDirectory { get; init; }
    public required int Depth { get; init; }

    public string Proposed { get; set; } = "";

    /// <summary>Predlog posle transliteracije, pre eventualnog sufiksa za konflikt.</summary>
    public string BaseProposed { get; set; } = "";

    /// <summary>Red koji prijavljuje da sadržaj foldera nije mogao da se pročita (nije stavka za preimenovanje).</summary>
    public bool IsListingError { get; init; }

    /// <summary>Korisnikov izbor (kolona sa kvačicom).</summary>
    public bool Include { get; set; }

    // rezultat provere naziva (ne zavisi od drugih stavki)
    public Validation Base { get; set; }

    // konačni status posle provere konflikata i dužine putanje, ili posle izvršavanja
    public ItemStatus Status { get; set; }
    public string Message { get; set; } = "";

    /// <summary>Najduža putanja ispod foldera (originalna), za proveru 260 znakova. -1 = nije izmereno.</summary>
    public int MaxDescendantPathLength { get; set; } = -1;

    public string ProposedFullPath => Path.Combine(ParentDir, Proposed);

    /// <summary>Stavka bi se preimenovala ako je korisnik ostavio kvačicu i provera to dozvoljava.</summary>
    public bool CanRename =>
        !IsListingError && Proposed != Name &&
        (Base.Status == ItemStatus.Ready || (Base.Status == ItemStatus.Invalid && Base.Overridable));
}
