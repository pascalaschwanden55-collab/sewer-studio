using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Art des im Dateinamen gefundenen Bezugs.</summary>
public enum ImportPdfReferenceKind
{
    Haltung,
    Schacht
}

/// <summary>
/// Gefundener Bezug eines Import-PDFs. <see cref="Name"/> ist immer der im Projekt
/// bereits vorhandene Haltungs- bzw. Schachtname, nie der Rohtext aus dem Dateinamen.
/// </summary>
public readonly record struct ImportPdfReference(ImportPdfReferenceKind Kind, string Name);

/// <summary>
/// Ordnet einen PDF-Dateinamen einer BEREITS VORHANDENEN Haltung oder einem bereits
/// vorhandenen Schacht zu.
///
/// Bewusst fail-closed: Der Dienst legt nichts an und raet nicht. Er liefert nur dann
/// einen Treffer, wenn genau ein bekannter Name im Dateinamen steht. Damit koennen
/// Herstellernamen wie "Section_8_892037-74091.pdf" verwendet werden, ohne dass
/// beliebige Zahlenfolgen zu Geister-Haltungen werden.
/// </summary>
public interface IImportPdfReferenceResolver
{
    /// <param name="fileName">Dateiname des PDFs (mit oder ohne Pfad).</param>
    /// <param name="haltungsnamen">Im Projekt vorhandene Haltungsnamen.</param>
    /// <param name="schachtnummern">Im Projekt vorhandene Schachtnummern.</param>
    /// <returns>Der eindeutige Bezug oder null, wenn keiner oder mehrere gefunden wurden.</returns>
    ImportPdfReference? Resolve(
        string fileName,
        IReadOnlyCollection<string> haltungsnamen,
        IReadOnlyCollection<string> schachtnummern);
}
