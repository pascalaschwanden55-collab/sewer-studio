using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>Eine Haltung als Polylinie in LV95 (EPSG:2056). X=Ostwert(C1), Y=Nordwert(C2).</summary>
public sealed record HaltungGeometry(string Haltungsname, IReadOnlyList<(double X, double Y)> Points);
