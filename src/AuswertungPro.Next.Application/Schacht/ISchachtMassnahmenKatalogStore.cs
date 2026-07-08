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
    IReadOnlyList<SchachtMassnahmeKatalogEintrag> Load();

    void Save(IEnumerable<SchachtMassnahmeKatalogEintrag> eintraege);
}
