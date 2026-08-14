using System;
using System.Diagnostics;
using System.IO;
using AuswertungPro.Next.Application.Output;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

/// <summary>
/// Uebergibt eine PDF-Datei an den Druckweg des Betriebssystems.
///
/// Zieht den <c>Process.Start</c>-Aufruf aus dem <c>BuilderPageViewModel</c> heraus.
/// Der Pfad wird vorher geprueft: Fehlt die Datei, gibt es eine klare Meldung statt
/// einer Win32-Ausnahme aus der Tiefe der Shell.
/// </summary>
public sealed class PdfPrintService : IPdfPrintService
{
    private readonly Action<ProcessStartInfo> _start;

    public PdfPrintService()
        : this(psi => Process.Start(psi))
    {
    }

    /// <summary>Test-Naht: erlaubt die Pruefung ohne echten Prozessstart.</summary>
    internal PdfPrintService(Action<ProcessStartInfo> start)
        => _start = start ?? throw new ArgumentNullException(nameof(start));

    public void Print(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("Es wurde keine Datei angegeben.", nameof(pdfPath));

        if (!File.Exists(pdfPath))
            throw new FileNotFoundException($"Die Datei wurde nicht gefunden: {pdfPath}", pdfPath);

        _start(new ProcessStartInfo
        {
            FileName = pdfPath,
            Verb = "print",
            UseShellExecute = true
        });
    }
}
