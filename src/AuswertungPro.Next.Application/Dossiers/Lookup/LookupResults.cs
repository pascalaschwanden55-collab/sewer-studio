using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>Eine Gemeinde mit ihrer BFS-Nummer.</summary>
public sealed record Municipality(int BfsNr, string Name);

/// <summary>
/// Eine Liegenschaft aus dem Parzellendienst. <paramref name="OutlineWkt"/> ist
/// der Umriss als WKT-Polygon in EPSG:2056 und wird fuer die raeumliche Suche
/// nach Leitungen gebraucht.
/// </summary>
public sealed record ParcelInfo(
    string Number,
    int BfsNr,
    string Municipality,
    int? AreaSqm,
    string Egrid,
    string OutlineWkt,
    string LandRegistryUrl);

/// <summary>
/// Ein Eigentuemer laut Grundbuchauskunft. <paramref name="Designation"/> ist
/// die Kennzeichnung bei Miteigentum ("Lit.A"), sonst leer.
/// </summary>
public sealed record LandRegistryOwner(
    string Designation,
    string Name,
    string AddressLine,
    string Share);

/// <summary>
/// Der Auszug einer Liegenschaft. <paramref name="NoOwnerRegistered"/> ist wahr,
/// wenn die Auskunft ausdruecklich "Keine" meldet — das gibt es wirklich und
/// darf nie als Name durchgehen.
/// </summary>
public sealed record LandRegistryEntry(
    string BuildingStreet,
    string BuildingHouseNumber,
    string PostalCode,
    string Town,
    IReadOnlyList<LandRegistryOwner> Owners,
    bool NoOwnerRegistered);

/// <summary>
/// Eine Haltung aus dem Abwassernetz des Kantons. <paramref name="Owner"/> ist
/// die Eigentuemerangabe des Dienstes, zum Beispiel "Privat".
/// </summary>
public sealed record NetworkHolding(
    string Designation,
    string Owner,
    double? LengthMeters,
    string GeometryWkt)
{
    /// <summary>Nur private Leitungen gehoeren in ein Eigentuemerdossier.</summary>
    public bool IsPrivate
        => Owner?.Contains("Privat", System.StringComparison.OrdinalIgnoreCase) ?? false;
}
