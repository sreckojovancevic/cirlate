namespace Cirlat.Core;

public static class SystemInfo
{
    /// <summary>
    /// CIRLAT radi bez elevacije: eleviran proces bi imao pristup sistemskim lokacijama
    /// koje korisnik inače ne može da menja, a zaštita ne sme da zavisi samo od prava pristupa.
    /// </summary>
    public static bool IsElevated()
    {
        try { return Native.IsSplitTokenElevated(); }
        catch { return false; }
    }
}
