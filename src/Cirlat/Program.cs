using Cirlat.Core;

namespace Cirlat;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        if (SystemInfo.IsElevated())
        {
            MessageBox.Show(
                "CIRLAT je pokrenut sa administratorskim pravima.\n\nPokreni ga kao običan korisnik.",
                "CIRLAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ProtectedPaths prot;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            prot = ProtectedPaths.Build();
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }

        Application.Run(new MainForm(prot));
    }
}
