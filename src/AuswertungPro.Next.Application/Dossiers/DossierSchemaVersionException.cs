using System;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Wird geworfen, wenn eine gespeicherte Dossier-Datei eine neuere
/// Formatversion traegt als diese Programmversion kennt.
///
/// Bewusst ein eigener Typ statt einer allgemeinen
/// <see cref="InvalidOperationException"/>: der Store faengt bei einem
/// Lesefehler normalerweise jede Ausnahme ab und laedt still ein vorhandenes
/// ".bak". Genau das darf bei einer zu neuen Formatversion NICHT passieren —
/// sonst ueberschreibt die naechste Aenderung die neuere Datei mit dem
/// aelteren Backup-Inhalt. Der Store erkennt diesen Typ und laesst ihn
/// unveraendert durch.
/// </summary>
public sealed class DossierSchemaVersionException : InvalidOperationException
{
    public DossierSchemaVersionException(string message)
        : base(message)
    {
    }

    public DossierSchemaVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
