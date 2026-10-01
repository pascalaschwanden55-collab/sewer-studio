using System;
using System.Threading;
using AuswertungPro.Next.Application.Export;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>Begrenzt den Druck auf den gefuellten Bereich des einzigen Berichtblatts.</summary>
internal static class ExcelArbeitsansicht
{
    public static void Abschliessen(
        IXLWorksheet liste, int kopf, int erste, int letzte, int spalten,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Fremde Vorlagen behalten ihren bestehenden Vertrag.
        if (kopf != ExcelVorlagenLayout.KopfZeile
            || erste != ExcelVorlagenLayout.ErsteDatenZeile
            || liste.Cell(24, 1).GetString() != "Aktuelle Auswahl")
            return;

        liste.PageSetup.PrintAreas.Clear();
        liste.PageSetup.PrintAreas.Add(1, 1, Math.Max(kopf, letzte), spalten);
        liste.PageSetup.Header.Left.AddText(liste.Cell(25, 1).GetString());
    }
}
