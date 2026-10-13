using System.Runtime.InteropServices;

namespace Cirlat.Core;

internal static unsafe partial class Native
{
    [LibraryImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial uint QueryDosDevice(string? deviceName, char* targetPath, uint ucchMax);

    [LibraryImport("kernel32.dll", EntryPoint = "GetLongPathNameW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial uint GetLongPathName(string shortPath, char* longPath, uint cchBuffer);

    [LibraryImport("mpr.dll", EntryPoint = "WNetGetConnectionW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int WNetGetConnection(string localName, char* remoteName, ref int length);

    [LibraryImport("shell32.dll")]
    public static partial int SHGetKnownFolderPath(in Guid rfid, uint flags, nint token, out nint path);

    // elevacija
    [LibraryImport("kernel32.dll")]
    public static partial nint GetCurrentProcess();

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(nint token, int infoClass, void* info, uint length, out uint returnLength);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    public static string? KnownFolder(Guid id)
    {
        if (SHGetKnownFolderPath(id, 0, 0, out nint p) != 0) return null;
        try { return Marshal.PtrToStringUni(p); }
        finally { Marshal.FreeCoTaskMem(p); }
    }

    public static readonly Guid FolderDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    public static string? QueryDosDeviceFirst(string device)
    {
        var buf = new char[4096];
        fixed (char* p = buf)
        {
            uint n = QueryDosDevice(device, p, (uint)buf.Length);
            if (n == 0) return null;
            int end = Array.IndexOf(buf, '\0');
            return end <= 0 ? null : new string(buf, 0, end);
        }
    }

    public static string? NetworkTarget(string device)
    {
        int len = 1024;
        var buf = new char[len];
        fixed (char* p = buf)
        {
            if (WNetGetConnection(device, p, ref len) != 0) return null;
            int end = Array.IndexOf(buf, '\0');
            return end <= 0 ? null : new string(buf, 0, end);
        }
    }

    public static string? LongName(string path)
    {
        var buf = new char[32768];
        fixed (char* p = buf)
        {
            uint n = GetLongPathName(path, p, (uint)buf.Length);
            if (n == 0 || n >= buf.Length) return null;
            return new string(buf, 0, (int)n);
        }
    }

    /// <summary>True ako proces radi sa eleviranim delom podeljenog UAC tokena.</summary>
    public static bool IsSplitTokenElevated()
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0008, out var token)) return false;
        try
        {
            int type = 0;
            return GetTokenInformation(token, 18, &type, sizeof(int), out _) && type == 2;
        }
        finally { CloseHandle(token); }
    }
}
