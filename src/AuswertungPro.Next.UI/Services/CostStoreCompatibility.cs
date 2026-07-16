using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Unveraenderliche Uebergangsfabrik fuer bestehende oeffentliche Konstruktoren.
/// Der normale Programmweg bezieht die Fabrik weiterhin aus dem ServiceProvider.
/// </summary>
internal static class CostStoreCompatibility
{
    internal static ICostStoreFactory Factory { get; } = new CostStoreFactory();
}
