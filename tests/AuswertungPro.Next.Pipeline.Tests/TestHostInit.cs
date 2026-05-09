using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AuswertungPro.Next.Pipeline.Tests;

// Workaround fuer FirebirdSql.Data.FirebirdClient (10.0 .. 10.3.4):
// FirebirdSql registriert in seinem static cctor einen
// AppDomain.ProcessExit-Handler (ShutdownHelper), der auf den nativen
// fb_shutdown ruft. Wenn fbclient.dll im Test-Output fehlt (und das ist
// hier der Fall — Tests benutzen Firebird nie), wirft der Aufruf eine
// BadImageFormatException (0x8007000B), die nicht abgefangen wird, weil
// sie aus einem ProcessExit-Handler kommt. Resultat: testhost.exe
// terminiert mit unhandled exception → Windows zeigt WerFault-Popup.
//
// Dieser ModuleInitializer setzt fuer den Test-Prozess SEM_NOGPFAULTERRORBOX,
// sodass Windows den Crash-Dialog NICHT mehr anzeigt. Der Crash selbst
// passiert weiterhin (steht im Application Event Log), die Test-Resultate
// sind aber nicht betroffen.
//
// Sobald FirebirdSql den Bug fixt, kann diese Datei wieder weg.
internal static class TestHostInit
{
    [ModuleInitializer]
    internal static void SuppressWerFaultDialog()
    {
        try
        {
            // SEM_FAILCRITICALERRORS = 0x0001  → keine "drive not ready" Popups
            // SEM_NOGPFAULTERRORBOX  = 0x0002  → kein Crash-Dialog beim Process-Exit
            SetErrorMode(0x0001 | 0x0002);
        }
        catch
        {
            // best effort — kein kritischer Pfad
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);
}
