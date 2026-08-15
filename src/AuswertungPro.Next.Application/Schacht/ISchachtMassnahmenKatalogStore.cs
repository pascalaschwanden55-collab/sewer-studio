using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Schacht;

/// <summary>
/// Laedt/speichert die global (projektuebergreifend) gepflegte Schacht-Massnahmen-Liste
/// (Name + manueller Preis). Die Implementierung legt eine JSON-Datei unter %AppData%
/// ab — analog den bestehenden Dropdown-Listen.
/// </summary>
public interface ISchachtMassnahmenKatalogStore
{
    /// <summary>
    /// Laedt die Liste. Eine fehlende Datei ist ein Erstlauf und liefert die
    /// Standardliste mit <paramref name="loadError"/> = <c>null</c>. Eine vorhandene,
    /// aber unlesbare Datei setzt <paramref name="loadError"/>; der Aufrufer muss
    /// Bearbeiten und Speichern dann sperren, sonst ersetzt die Standardliste die
    /// selbst gepflegten Eintraege (Audit 2026-08-14, M2).
    /// </summary>
    IReadOnlyList<SchachtMassnahmeKatalogEintrag> Load(out string? loadError);

    /// <summary>
    /// Speichert die Liste. Liefert <c>false</c> mit <paramref name="error"/>, wenn das
    /// vorhandene Ziel nicht sicher gelesen werden kann — dann wird nichts geschrieben.
    /// </summary>
    bool Save(IEnumerable<SchachtMassnahmeKatalogEintrag> eintraege, out string? error);
}
