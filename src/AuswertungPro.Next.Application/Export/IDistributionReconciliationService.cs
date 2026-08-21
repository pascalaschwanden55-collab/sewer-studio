using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Export;

/// <summary>Ein Eintrag im Verteilordner, der im Projekt kein Gegenstueck hat.</summary>
/// <param name="RelativePath">Pfad ab Projektwurzel, z. B. <c>Haltungen_Verteilt\99999-88888</c>.</param>
/// <param name="IsDirectory">Ordner oder lose Datei.</param>
public sealed record DistributionReconciliationEntry(string RelativePath, bool IsDirectory);

/// <summary>
/// Ergebnis der Vorschau. Der Plan veraendert nichts - er sagt nur, was der Abgleich
/// bewegen wuerde.
/// </summary>
/// <param name="ToMove">Eintraege ohne Gegenstueck im Projekt.</param>
/// <param name="Skipped">
/// Bewusst nicht angefasste Eintraege samt Grund (Verknuepfungen, unlesbare Pfade).
/// Im Zweifel wird nichts bewegt, sondern gemeldet.
/// </param>
/// <param name="BlockedReason">
/// Gesetzt, wenn der Abgleich gar nicht laufen darf - etwa bei leerem Projekt.
/// Dann ist <see cref="ToMove"/> leer.
/// </param>
public sealed record DistributionReconciliationPlan(
    IReadOnlyList<DistributionReconciliationEntry> ToMove,
    IReadOnlyList<string> Skipped,
    string? BlockedReason);

/// <summary>Was der Abgleich tatsaechlich bewegt hat.</summary>
public sealed record DistributionReconciliationResult(
    int MovedDirectories,
    int MovedFiles,
    string? TrashFolderRelative,
    IReadOnlyList<string> Messages);

/// <summary>
/// "Abgleichen": In <c>Haltungen_Verteilt</c> und <c>Schächte_Verteilt</c> soll nur
/// liegen, wozu es im Projekt eine Haltung bzw. einen Schacht gibt. Alles andere
/// wandert in den Papierkorb des Projekts.
///
/// Bewusst zweistufig wie der XTF-Revisions-Export: erst <see cref="Plan"/> zum Ansehen,
/// dann <see cref="Apply"/> nach ausdruecklicher Bestaetigung. Verschoben wird immer,
/// geloescht nie.
/// </summary>
public interface IDistributionReconciliationService
{
    /// <summary>Ermittelt schreibfrei, was kein Gegenstueck im Projekt hat.</summary>
    DistributionReconciliationPlan Plan(string projectFolder, Project project);

    /// <summary>
    /// Verschiebt die Eintraege des Plans in
    /// <c>Papierkorb\JJJJ-MM-TT_HHMMSS\&lt;Verteilordner&gt;\...</c>.
    /// Ein gesperrter Plan bewegt nichts.
    /// </summary>
    /// <param name="nowLocal">Zeitpunkt fuer den Namen des Laufordners.</param>
    DistributionReconciliationResult Apply(
        string projectFolder,
        DistributionReconciliationPlan plan,
        DateTime nowLocal);
}
