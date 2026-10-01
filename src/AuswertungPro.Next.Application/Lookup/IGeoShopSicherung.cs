using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Schreibt einen vollstaendigen, wiederherstellbaren Vorherstand einschliesslich ungespeicherter Eingaben.</summary>
public interface IGeoShopSicherung
{
    string Sichere(Project projekt);
}
