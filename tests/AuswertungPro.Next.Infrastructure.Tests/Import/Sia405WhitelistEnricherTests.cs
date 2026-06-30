using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer Sia405WhitelistEnricher: kontrollierte Anreicherung leerer Felder
/// aus dem SIA405-XTF-Datensatz, ohne userEdited-Werte oder geschuetzte Felder zu ueberschreiben.
/// </summary>
public sealed class Sia405WhitelistEnricherTests
{
    // --- Hilfsmethoden ---

    private static Project ErzeugeTestProjekt(params (string haltungsname, string rohrmaterial)[] haltungen)
    {
        var projekt = new Project();
        foreach (var (name, rohrmaterial) in haltungen)
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
            if (!string.IsNullOrEmpty(rohrmaterial))
                record.SetFieldValue("Rohrmaterial", rohrmaterial, FieldSource.Manual, userEdited: false);
            projekt.Data.Add(record);
        }
        return projekt;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ErzeugeSia405(
        string haltungsname, params (string feld, string wert)[] felder)
    {
        var feldDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (feld, wert) in felder)
            feldDict[feld] = wert;

        return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [haltungsname] = feldDict
        };
    }

    // --- Testfall (a): Leeres Feld wird aus SIA405 gefüllt ---

    [Fact]
    public void LeeresFeld_WirdAusSia405Gefuellt()
    {
        // Arrange: Haltung mit leerem Rohrmaterial
        var projekt = ErzeugeTestProjekt(("H-001", ""));
        var sia405 = ErzeugeSia405("H-001", ("Rohrmaterial", "PVC"));

        // Act
        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        // Assert: Feld gesetzt, Filled=1, keine Konflikte
        var record = projekt.Data[0];
        Assert.Equal("PVC", record.GetFieldValue("Rohrmaterial"));
        Assert.Equal(1, ergebnis.Filled);
        Assert.Empty(ergebnis.Conflicts);
    }

    // --- Testfall (b): Abweichendes gefuelltes Feld erzeugt Konflikt, Wert bleibt ---

    [Fact]
    public void GefuelltesFeld_AbweichendVonSia405_BeibtMitKonfliktzeile()
    {
        // Arrange: Haltung mit vorhandenem Rohrmaterial "Beton", SIA405 liefert "PVC"
        var projekt = ErzeugeTestProjekt(("H-002", "Beton"));
        var sia405 = ErzeugeSia405("H-002", ("Rohrmaterial", "PVC"));

        // Act
        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        // Assert: Feld unveraendert, Filled=0, eine Konflikt-Zeile
        var record = projekt.Data[0];
        Assert.Equal("Beton", record.GetFieldValue("Rohrmaterial"));
        Assert.Equal(0, ergebnis.Filled);
        Assert.Single(ergebnis.Conflicts);
        Assert.Contains("H-002", ergebnis.Conflicts[0]);
        Assert.Contains("Rohrmaterial", ergebnis.Conflicts[0]);
        Assert.Contains("Beton", ergebnis.Conflicts[0]);
        Assert.Contains("PVC", ergebnis.Conflicts[0]);
    }

    // --- Testfall (c): Geschuetztes Feld Datum_Jahr wird NIEMALS gesetzt ---

    [Fact]
    public void GeschuetzteFeld_DatumJahr_WirdNieGesetzt()
    {
        // Arrange: Haltung mit leerem Datum_Jahr, SIA405 liefert einen Wert
        var projekt = ErzeugeTestProjekt(("H-003", ""));
        // Datum_Jahr im Record ist leer (default)
        var sia405 = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["H-003"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Datum_Jahr"] = "2025",
                ["Rohrmaterial"] = "Steinzeug"
            }
        };

        // Act
        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        // Assert: Datum_Jahr bleibt leer, Rohrmaterial wird gefuellt
        var record = projekt.Data[0];
        Assert.Equal("", record.GetFieldValue("Datum_Jahr"));
        Assert.Equal("Steinzeug", record.GetFieldValue("Rohrmaterial"));
        Assert.Equal(1, ergebnis.Filled); // nur Rohrmaterial geaehlt
    }

    // --- Testfall (d): UserEdited-Feld bleibt unveraendert ---

    [Fact]
    public void UserEditedFeld_BleibtUnangetastet()
    {
        // Arrange: Rohrmaterial vom Benutzer editiert
        var projekt = ErzeugeTestProjekt(("H-004", ""));
        var record = projekt.Data[0];
        // Benutzer hat Rohrmaterial explizit auf "GFK" gesetzt
        record.SetFieldValue("Rohrmaterial", "GFK", FieldSource.Manual, userEdited: true);

        var sia405 = ErzeugeSia405("H-004", ("Rohrmaterial", "PVC"));

        // Act
        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        // Assert: Wert bleibt "GFK" (userEdited-Schutz im Modell greift),
        // kein Konflikt (SetFieldValue war ein No-Op), Filled=0
        Assert.Equal("GFK", record.GetFieldValue("Rohrmaterial"));
        Assert.Equal(0, ergebnis.Filled);
        // Kein Konflikt: das Modell verwirft den Aufruf still (kein Schreiben = kein Konflikt-Check)
        Assert.Empty(ergebnis.Conflicts);
    }

    // --- Zusatz: Gleicher Wert im Record und SIA405 — kein Filled, kein Konflikt ---

    [Fact]
    public void GleicherWert_KeinFilledKeinKonflikt()
    {
        var projekt = ErzeugeTestProjekt(("H-005", "PVC"));
        var sia405 = ErzeugeSia405("H-005", ("Rohrmaterial", "PVC"));

        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        Assert.Equal("PVC", projekt.Data[0].GetFieldValue("Rohrmaterial"));
        Assert.Equal(0, ergebnis.Filled);
        Assert.Empty(ergebnis.Conflicts);
    }

    // --- Zusatz: Haltungsname-Vergleich ist case-insensitiv ---

    [Fact]
    public void HaltungsnameLookup_IstCaseInsensitiv()
    {
        var projekt = ErzeugeTestProjekt(("h-006", ""));
        var sia405 = ErzeugeSia405("H-006", ("Rohrmaterial", "PE"));

        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        Assert.Equal("PE", projekt.Data[0].GetFieldValue("Rohrmaterial"));
        Assert.Equal(1, ergebnis.Filled);
    }

    // --- Zusatz: Haltung ohne SIA405-Eintrag wird uebersprungen ---

    [Fact]
    public void HaltungOhneSia405Eintrag_WirdUebersprungen()
    {
        var projekt = ErzeugeTestProjekt(("H-007", ""));
        // Leere SIA405-Map
        var sia405 = new Dictionary<string, IReadOnlyDictionary<string, string>>();

        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        Assert.Equal("", projekt.Data[0].GetFieldValue("Rohrmaterial"));
        Assert.Equal(0, ergebnis.Filled);
        Assert.Empty(ergebnis.Conflicts);
    }

    // --- Robustheit: Apply ist case-insensitiv AUCH wenn Aufrufer Default-Comparer verwendet ---
    // Szenario: Aufrufer baut die Map mit new Dictionary<>() (Ordinal/case-sensitiv).
    // Der Haltungsname im Record unterscheidet sich nur in der Gross-/Kleinschreibung.
    // Apply muss trotzdem treffen (interner ci-Lookup).

    [Fact]
    public void Apply_MitDefaultComparerMap_FindetHaltungCaseInsensitiv()
    {
        // Arrange: Record mit Kleinschreibung "h-008", Map mit Grossschreibung "H-008"
        // Die Map wird bewusst mit Default-Comparer (case-sensitiv) gebaut.
        var projekt = ErzeugeTestProjekt(("h-008", ""));
        var feldDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Rohrmaterial"] = "Beton"
        };
        // Default-Comparer — wuerde bei direktem TryGetValue("h-008") NICHT treffen
        var sia405 = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["H-008"] = feldDict
        };

        // Act
        var ergebnis = Sia405WhitelistEnricher.Apply(projekt, sia405);

        // Assert: Anreicherung muss trotzdem greifen
        Assert.Equal("Beton", projekt.Data[0].GetFieldValue("Rohrmaterial"));
        Assert.Equal(1, ergebnis.Filled);
        Assert.Empty(ergebnis.Conflicts);
    }
}
