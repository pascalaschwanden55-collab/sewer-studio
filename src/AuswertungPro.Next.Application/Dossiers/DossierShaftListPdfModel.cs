using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Vollstaendige, bereits zugeordnete Anzeige-Daten fuer die Schachtliste.
/// Der PDF-Dienst muss dadurch weder Projektdaten suchen noch Dossierregeln kennen.
/// </summary>
public sealed record DossierShaftListPdfModel(
    string DossierName,
    string OwnerName,
    string PropertyAddress,
    DateTime Stand,
    IReadOnlyList<DossierShaftLine> Shafts,
    int MissingShaftCount);

/// <summary>Bildet die Kopf- und Zeilendaten der Schachtliste ohne Dateizugriff.</summary>
public static class DossierShaftListPdfModelBuilder
{
    public static DossierShaftListPdfModel Build(
        DossierDefinition dossier,
        DossierSnapshot snapshot,
        DateTime stand)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(snapshot);

        var owner = Trim(dossier.OwnerName);
        if (owner.Length == 0)
        {
            owner = dossier.Owners?
                .Where(row => row is not null)
                .Select(row => Trim(row.Name))
                .FirstOrDefault(name => name.Length > 0)
                ?? string.Empty;
        }

        var street = Trim(dossier.Address);
        var houseNumbers = Trim(dossier.HouseNumbers);
        if (houseNumbers.Length > 0
            && !street.Contains(houseNumbers, StringComparison.OrdinalIgnoreCase))
        {
            street = Join(street, houseNumbers);
        }

        var location = Join(Trim(dossier.PostalCode), Trim(dossier.Town));
        var property = Join(street, location, separator: ", ");
        if (property.Length == 0)
            property = Trim(dossier.Name);

        return new DossierShaftListPdfModel(
            Trim(dossier.Name),
            owner,
            property,
            stand,
            snapshot.Shafts,
            snapshot.MissingShaftNumbers.Count);
    }

    private static string Trim(string? value) => (value ?? string.Empty).Trim();

    private static string Join(string left, string right, string separator = " ")
    {
        if (left.Length == 0)
            return right;
        if (right.Length == 0)
            return left;
        return left + separator + right;
    }
}
