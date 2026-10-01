using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Eine ausdruecklich ausgewaehlte Datei verknuepfen, ohne daraus Schachtdaten zu erfinden.</summary>
public static class SchachtPdfVerknuepfung
{
    public static string Name(SchachtRecord schacht)
        => schacht.GetFieldValue(SchachtFeldnamen.Feld(schacht, "Schachtnummer")).Trim();

    public static void Verknuepfe(SchachtRecord schacht, string projektpfad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projektpfad);
        // Die Dateiauswahl ist eine bewusste Benutzeraktion. Beobachtungen und Fachwerte bleiben bestehen.
        schacht.SetFieldValue(FieldKeys.PdfPath, projektpfad, FieldSource.Manual, userEdited: true);
    }
}
