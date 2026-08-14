namespace AuswertungPro.Next.Application.Output;

/// <summary>
/// Startet einen Druckauftrag fuer eine vorhandene PDF-Datei.
///
/// Eigener Vertrag, damit im ViewModel kein <c>Process.Start</c> mehr steht: Der
/// Prozessstart ist Infrastruktur und war so weder ersetzbar noch pruefbar.
/// </summary>
public interface IPdfPrintService
{
    /// <summary>
    /// Uebergibt die Datei an den Druckweg des Betriebssystems.
    /// Wirft, wenn die Datei fehlt oder der Druckauftrag nicht startet.
    /// </summary>
    void Print(string pdfPath);
}
