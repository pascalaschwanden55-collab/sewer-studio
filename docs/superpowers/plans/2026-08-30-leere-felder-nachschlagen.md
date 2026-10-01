# Leere Felder per Rechtsklick nachschlagen — Umsetzungsplan

> **Fuer agentische Arbeiter:** ERFORDERLICHER SUB-SKILL: Nutze
> `superpowers:subagent-driven-development` (empfohlen) oder
> `superpowers:executing-plans`, um diesen Plan Aufgabe fuer Aufgabe
> umzusetzen. Schritte verwenden Checkbox-Syntax (`- [ ]`).

**Ziel:** Ein Rechtsklick in ein leeres Schachtfeld schlaegt den Wert beim
Kanton nach, zeigt ihn als Vorschlag und uebernimmt ihn erst nach
ausdruecklicher Bestaetigung — mit nachvollziehbarer Herkunft.

**Architektur:** Ein gemeinsamer Vertrag `IFeldWertNachschlag` mit zwei
Anbietern. Der Kataster-Anbieter liest eine lokal aufgebaute Schacht-Tabelle
aus der Kataster-XTF; der Grundbuch-Anbieter setzt auf den dort gefundenen
Koordinaten auf und verkettet die bestehenden Dossier-Dienste. Ein UseCase
waehlt anhand des Feldnamens die Quelle und schreibt selbst nichts.

**Tech Stack:** C# / .NET 10, WPF, xUnit. Keine neuen NuGet-Pakete.

**Spec:** `docs/superpowers/specs/2026-08-30-leere-felder-nachschlagen-design.md`

## Globale Randbedingungen

- **SewerStudio muss beim Bauen geschlossen sein.** Ein laufendes Programm
  sperrt die DLLs; dann testet man einen alten Stand.
- Build: `dotnet build AuswertungPro.sln` — ohne Fehler, ohne neue Warnungen.
- Test: `dotnet test AuswertungPro.sln` — vollstaendig gruen.
- Kommentare und Benutzertexte auf Deutsch (Projektregel).
- **Kein Sammellauf.** Es darf keinen Befehl geben, der mehrere Felder auf
  einmal nachschlaegt. Die Grundbuchauskunft erlaubt nur Einzelabfragen.
- **Nur leere Felder.** Der Menuepunkt erscheint nie an einem gefuellten Feld.
- **`userEdited: true` ist Pflicht** beim Schreiben — es ist der einzige
  Schutz vor dem naechsten Import (siehe Aufgabe 4).
- **Keine Personennamen im Log.** Nur Status, Dauer, Fehlerklasse.
- Neue Dienste bekommen ein Interface und werden im `ServiceProvider`
  registriert; kein `new` verstreut im Code.
- `MergeEngine`, QGIS-Bruecke, Dossier-Weg und Import werden nicht angefasst.

## Achtung: parallele Arbeit

Beim Schreiben dieses Plans waren folgende Dateien in Bearbeitung:
`SchachtansichtView.xaml`, `SchachtansichtView.xaml.cs`,
`HaltungsansichtView.xaml(.cs)`, `OverviewPage.xaml`,
`OverviewPageViewModel.cs`, `DossiersPage.xaml` und vier Testdateien.

**Aufgabe 5 fasst die Schachtansicht an.** Vor ihrem Beginn den Stand mit
`git status` erneut pruefen und bei laufender Fremdarbeit Ruecksprache halten.
Die Aufgaben 1 bis 4 beruehren keine dieser Dateien.

## Dateistruktur

**Stufe 1 — Kataster (Aufgaben 1 bis 5):**

- Create: `src/AuswertungPro.Next.Infrastructure/Map/SchachtCadastreExtractor.cs`
  — Datensatz `CadastreSchacht` und statische Fassade, Vorbild
  `HaltungCadastreExtractor`
- Create: `src/AuswertungPro.Next.Infrastructure/Map/ISchachtCadastreTableStore.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Map/SchachtCadastreTableFileStore.cs`
  — XTF lesen, Tabelle schreiben/lesen, Frischepruefung
- Create: `src/AuswertungPro.Next.Application/Lookup/FeldNachschlagVertrag.cs`
  — `IFeldWertNachschlag`, `FeldNachschlagAnfrage`, `FeldVorschlag`,
  `FeldNachschlagErgebnis`
- Create: `src/AuswertungPro.Next.Application/Lookup/KatasterPlatzhalter.cs`
- Create: `src/AuswertungPro.Next.Application/Lookup/FeldQuellenTabelle.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Lookup/KatasterFeldNachschlag.cs`
- Create: `src/AuswertungPro.Next.Application/UseCases/FeldNachschlagUseCase.cs`
- Modify: `src/AuswertungPro.Next.Domain/Models/FieldSource.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`,
  `ServiceProviderRegistrationMap.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsModels.cs`,
  `Views/Controls/RecordDetailsView.xaml`,
  `DataPage/SchaechteRecordDetailsBuilder.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Windows/FeldVorschlagWindow.xaml(.cs)`

**Stufe 2 — Grundbuch (Aufgaben 6 und 7):**

- Create: `src/AuswertungPro.Next.Infrastructure/Lookup/GrundbuchFeldNachschlag.cs`
- Create: `src/AuswertungPro.Next.Application/Lookup/PunktAlsKurzeLinie.cs`
- Modify: `FeldQuellenTabelle.cs`, `ServiceProvider.cs`

---

## Aufgabe 1: Schaechte aus der Kataster-XTF lesen

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Map/SchachtCadastreExtractor.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Map/ISchachtCadastreTableStore.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Map/SchachtCadastreTableFileStore.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/SchachtCadastreTableFileTests.cs`

**Interfaces:**
- Consumes: nichts (erste Aufgabe)
- Produces: `CadastreSchacht(string Bezeichnung, string? Funktion, string? Material,
  string? Dimension1, string? Dimension2, string? Status, double? Ost, double? Nord)`
  sowie `ISchachtCadastreTableStore` mit
  `IEnumerable<CadastreSchacht> Extract(string xtfPath)`,
  `int BuildTable(string xtfPath, string outTablePath)`,
  `IReadOnlyList<CadastreSchacht> ReadTable(string tablePath)`,
  `bool IsTableFresh(string tablePath, string xtfPath)`.

**Fachlicher Hintergrund:** In der XTF stehen die Fachdaten am `Normschacht`,
die Koordinaten aber am `Abwasserknoten`. Beide tragen dieselbe
`Bezeichnung`; der Knoten verweist zusaetzlich per `AbwasserbauwerkRef` auf
den Schacht. Der Extraktor liest beide Elementarten und fuehrt sie ueber die
Bezeichnung zusammen.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

Datei `tests/AuswertungPro.Next.Infrastructure.Tests/SchachtCadastreTableFileTests.cs`:

```csharp
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtCadastreTableFileTests
{
    private const string XtfAusschnitt = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
<DATASECTION>
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="chB0000000000001">
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht TID="ch1000a00000c3e1">
<Bezeichnung>80401</Bezeichnung>
<Funktion>Kontroll_Einsteigschacht</Funktion>
<Material>Beton</Material>
<Dimension1>1000</Dimension1>
<Dimension2>1000</Dimension2>
<Status>in_Betrieb</Status>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht>
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten TID="ch1000b00000c3e1">
<Bezeichnung>80401</Bezeichnung>
<AbwasserbauwerkRef REF="ch1000a00000c3e1" />
<Lage><COORD><C1>2692606.892</C1><C2>1192380.717</C2></COORD></Lage>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten>
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Deckel TID="ch1000c00000c3e1">
<Bezeichnung>DE_80401</Bezeichnung>
<Lage><COORD><C1>9999999.999</C1><C2>8888888.888</C2></COORD></Lage>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Deckel>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
</DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Extract_LiestFachdatenUndLageDesselbenSchachts()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var store = new SchachtCadastreTableFileStore();

            var schacht = store.Extract(xtf).Single();

            Assert.Equal("80401", schacht.Bezeichnung);
            Assert.Equal("Kontroll_Einsteigschacht", schacht.Funktion);
            Assert.Equal("Beton", schacht.Material);
            Assert.Equal("in_Betrieb", schacht.Status);
            Assert.Equal(2692606.892, schacht.Ost!.Value, 3);
            Assert.Equal(1192380.717, schacht.Nord!.Value, 3);
        }
        finally { File.Delete(xtf); }
    }

    [Fact]
    public void Extract_UebernimmtNiemalsDieLageDesDeckels()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var schacht = new SchachtCadastreTableFileStore().Extract(xtf).Single();

            // Der Deckel traegt eine andere, absichtlich auffaellige Lage.
            Assert.NotEqual(9999999.999, schacht.Ost!.Value, 3);
        }
        finally { File.Delete(xtf); }
    }

    [Fact]
    public void BuildTable_UndReadTable_LiefernDenselbenStand()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        var tabelle = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.tsv");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var store = new SchachtCadastreTableFileStore();

            var anzahl = store.BuildTable(xtf, tabelle);
            var gelesen = store.ReadTable(tabelle);

            Assert.Equal(1, anzahl);
            Assert.Equal("80401", gelesen.Single().Bezeichnung);
            Assert.Equal("Kontroll_Einsteigschacht", gelesen.Single().Funktion);
            Assert.Equal(2692606.892, gelesen.Single().Ost!.Value, 3);
            Assert.True(store.IsTableFresh(tabelle, xtf));
        }
        finally { File.Delete(xtf); File.Delete(tabelle); }
    }

    [Fact]
    public void IsTableFresh_ErkenntEineGeaenderteQuelle()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        var tabelle = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.tsv");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var store = new SchachtCadastreTableFileStore();
            store.BuildTable(xtf, tabelle);

            File.WriteAllText(xtf, XtfAusschnitt + "<!-- geaendert -->");

            Assert.False(store.IsTableFresh(tabelle, xtf));
        }
        finally { File.Delete(xtf); File.Delete(tabelle); }
    }
}
```

- [ ] **Schritt 2: Test laufen lassen, Fehlschlag pruefen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~SchachtCadastreTableFileTests"
```

Erwartet: Uebersetzungsfehler `CS0246: Der Typname "SchachtCadastreTableFileStore" wurde nicht gefunden`.

- [ ] **Schritt 3: Datensatz und Fassade anlegen**

`src/AuswertungPro.Next.Infrastructure/Map/SchachtCadastreExtractor.cs`:

```csharp
namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Ein Schacht aus dem amtlichen Abwasserkataster. Fachdaten stammen vom
/// Normschacht, die Lage vom gleichnamigen Abwasserknoten.
/// </summary>
public sealed record CadastreSchacht(
    string Bezeichnung,
    string? Funktion,
    string? Material,
    string? Dimension1,
    string? Dimension2,
    string? Status,
    double? Ost,
    double? Nord);

/// <summary>
/// Kompatible statische Fassade. Die Dateizugriffe liegen im injizierbaren
/// <see cref="ISchachtCadastreTableStore"/>.
/// </summary>
public static class SchachtCadastreExtractor
{
    private static readonly ISchachtCadastreTableStore Default =
        new SchachtCadastreTableFileStore();

    public const string TableHeader =
        "Bezeichnung\tFunktion\tMaterial\tDimension1\tDimension2\tStatus\tOst\tNord";

    public static ISchachtCadastreTableStore Current => Default;

    public static IEnumerable<CadastreSchacht> Extract(string xtfPath)
        => Current.Extract(xtfPath);

    public static int BuildTable(string xtfPath, string outTablePath)
        => Current.BuildTable(xtfPath, outTablePath);

    public static IReadOnlyList<CadastreSchacht> ReadTable(string tablePath)
        => Current.ReadTable(tablePath);

    public static bool IsTableFresh(string tablePath, string xtfPath)
        => Current.IsTableFresh(tablePath, xtfPath);
}
```

`src/AuswertungPro.Next.Infrastructure/Map/ISchachtCadastreTableStore.cs`:

```csharp
namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>Liest Schaechte aus der Kataster-XTF und haelt sie als Tabelle.</summary>
public interface ISchachtCadastreTableStore
{
    IEnumerable<CadastreSchacht> Extract(string xtfPath);
    int BuildTable(string xtfPath, string outTablePath);
    IReadOnlyList<CadastreSchacht> ReadTable(string tablePath);
    bool IsTableFresh(string tablePath, string xtfPath);
}
```

- [ ] **Schritt 4: Den Store umsetzen**

`src/AuswertungPro.Next.Infrastructure/Map/SchachtCadastreTableFileStore.cs`.
Aufbau und Hilfsmittel exakt wie in `HaltungCadastreTableFileStore` (dort
nachlesen: `AtomicTextFileWriter.Write`, Kopfzeile mit
`# source=...\tbytes=...\tmtimeUtc=...`, `Escape`, `NullIfEmpty`).

Der Lesekern:

```csharp
public IEnumerable<CadastreSchacht> Extract(string xtfPath)
{
    // Zwei Durchgaenge waeren teuer (die XTF ist 467 MB). Stattdessen ein
    // Durchgang: Fachdaten und Lagen getrennt sammeln, am Ende zusammenfuehren.
    var fach = new Dictionary<string, CadastreSchacht>(StringComparer.OrdinalIgnoreCase);
    var lagen = new Dictionary<string, (double Ost, double Nord)>(StringComparer.OrdinalIgnoreCase);

    var settings = new XmlReaderSettings
    {
        IgnoreWhitespace = true,
        IgnoreComments = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };
    using var reader = XmlReader.Create(xtfPath, settings);

    var inSchacht = false;
    var inKnoten = false;
    string? bezeichnung = null, funktion = null, material = null;
    string? dim1 = null, dim2 = null, status = null;
    double? ost = null, nord = null;
    var skipRead = false;

    while (skipRead || reader.Read())
    {
        skipRead = false;

        if (reader.NodeType == XmlNodeType.Element)
        {
            var local = reader.LocalName;

            if (local.EndsWith(".Normschacht", StringComparison.Ordinal))
            {
                inSchacht = true;
                bezeichnung = funktion = material = dim1 = dim2 = status = null;
            }
            else if (local.EndsWith(".Abwasserknoten", StringComparison.Ordinal))
            {
                inKnoten = true;
                bezeichnung = null; ost = null; nord = null;
            }
            else if ((inSchacht || inKnoten) && local == "Bezeichnung" && bezeichnung == null)
            {
                bezeichnung = reader.ReadElementContentAsString();
                skipRead = true;
            }
            else if (inSchacht && local == "Funktion" && funktion == null)
            {
                funktion = reader.ReadElementContentAsString(); skipRead = true;
            }
            else if (inSchacht && local == "Material" && material == null)
            {
                material = reader.ReadElementContentAsString(); skipRead = true;
            }
            else if (inSchacht && local == "Dimension1" && dim1 == null)
            {
                dim1 = reader.ReadElementContentAsString(); skipRead = true;
            }
            else if (inSchacht && local == "Dimension2" && dim2 == null)
            {
                dim2 = reader.ReadElementContentAsString(); skipRead = true;
            }
            else if (inSchacht && local == "Status" && status == null)
            {
                status = reader.ReadElementContentAsString(); skipRead = true;
            }
            else if (inKnoten && local == "C1" && ost == null)
            {
                var roh = reader.ReadElementContentAsString(); skipRead = true;
                if (double.TryParse(roh, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    ost = v;
            }
            else if (inKnoten && local == "C2" && nord == null)
            {
                var roh = reader.ReadElementContentAsString(); skipRead = true;
                if (double.TryParse(roh, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    nord = v;
            }
        }
        else if (reader.NodeType == XmlNodeType.EndElement)
        {
            var local = reader.LocalName;

            if (local.EndsWith(".Normschacht", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(bezeichnung))
                    fach[bezeichnung!] = new CadastreSchacht(
                        bezeichnung!, funktion, material, dim1, dim2, status, null, null);
                inSchacht = false;
                bezeichnung = null;
            }
            else if (local.EndsWith(".Abwasserknoten", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(bezeichnung) && ost.HasValue && nord.HasValue)
                    lagen[bezeichnung!] = (ost.Value, nord.Value);
                inKnoten = false;
                bezeichnung = null; ost = null; nord = null;
            }
        }
    }

    foreach (var (name, schacht) in fach)
    {
        yield return lagen.TryGetValue(name, out var lage)
            ? schacht with { Ost = lage.Ost, Nord = lage.Nord }
            : schacht;
    }
}
```

Wichtig: `inSchacht` und `inKnoten` werden am jeweiligen Endelement wieder
zurueckgesetzt. Ein `Deckel` ist ein Geschwister-Element und damit weder das
eine noch das andere — seine Lage wird nie gelesen. Genau das prueft der
zweite Test.

- [ ] **Schritt 5: Tests laufen lassen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~SchachtCadastreTableFileTests"
```

Erwartet: 4 bestanden.

- [ ] **Schritt 6: Committen**

```bash
git add src/AuswertungPro.Next.Infrastructure/Map/SchachtCadastre* \
        src/AuswertungPro.Next.Infrastructure/Map/ISchachtCadastreTableStore.cs \
        tests/AuswertungPro.Next.Infrastructure.Tests/SchachtCadastreTableFileTests.cs
git commit -m "feat(nachschlagen): Schaechte aus der Kataster-XTF lesen

Fachdaten vom Normschacht, Lage vom gleichnamigen Abwasserknoten. Ein
Durchgang durch die 467-MB-Datei, danach nur noch die Tabelle.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Aufgabe 2: Platzhalter erkennen und der Kataster-Anbieter

**Files:**
- Create: `src/AuswertungPro.Next.Application/Lookup/FeldNachschlagVertrag.cs`
- Create: `src/AuswertungPro.Next.Application/Lookup/KatasterPlatzhalter.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Lookup/KatasterFeldNachschlag.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/KatasterFeldNachschlagTests.cs`

**Interfaces:**
- Consumes: `CadastreSchacht`, `ISchachtCadastreTableStore` aus Aufgabe 1.
- Produces: `IFeldWertNachschlag` mit
  `Task<FeldNachschlagErgebnis> SucheAsync(FeldNachschlagAnfrage anfrage, CancellationToken ct = default)`;
  `FeldNachschlagAnfrage(string Schachtnummer, string Feldname)`;
  `FeldVorschlag(string Wert, string QuelleKlartext, string Herkunftshinweis)`;
  `FeldNachschlagErgebnis` mit den Zustaenden `Gefunden(FeldVorschlag)`,
  `Mehrdeutig(IReadOnlyList<FeldVorschlag>)`, `NichtGefunden(string Grund)`,
  `Gedrosselt()`, `Fehler(string Meldung)`;
  `KatasterPlatzhalter.IstPlatzhalter(string? wert)`.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

Datei `tests/AuswertungPro.Next.Infrastructure.Tests/KatasterFeldNachschlagTests.cs`:

```csharp
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KatasterFeldNachschlagTests
{
    private sealed class FesterStore : ISchachtCadastreTableStore
    {
        private readonly IReadOnlyList<CadastreSchacht> _schaechte;
        public FesterStore(params CadastreSchacht[] schaechte) => _schaechte = schaechte;

        public IEnumerable<CadastreSchacht> Extract(string xtfPath) => _schaechte;
        public int BuildTable(string xtfPath, string outTablePath) => _schaechte.Count;
        public IReadOnlyList<CadastreSchacht> ReadTable(string tablePath) => _schaechte;
        public bool IsTableFresh(string tablePath, string xtfPath) => true;
    }

    private static KatasterFeldNachschlag Baue(params CadastreSchacht[] schaechte)
        => new(new FesterStore(schaechte), tabellenPfad: "egal.tsv", xtfPfad: "egal.xtf");

    [Fact]
    public async Task Findet_die_Funktion_eines_bekannten_Schachts()
    {
        var dienst = Baue(new CadastreSchacht(
            "33429", "Kontroll_Einsteigschacht", "Beton", "1000", "1000", "in_Betrieb", 1.0, 2.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Kontroll_Einsteigschacht", vorschlag.Wert);
        Assert.Equal("Abwasserkataster", vorschlag.QuelleKlartext);
    }

    [Theory]
    [InlineData("unbekannt")]
    [InlineData("unbek.")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Platzhalter_gelten_als_nicht_gefunden(string platzhalter)
    {
        var dienst = Baue(new CadastreSchacht(
            "33429", platzhalter, "Beton", "1000", "1000", "in_Betrieb", 1.0, 2.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Unbekannte_Schachtnummer_meldet_nicht_gefunden()
    {
        var dienst = Baue(new CadastreSchacht(
            "33429", "Schlammsammler", null, null, null, null, 1.0, 2.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("99999", "Funktion"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Doppelte_Schachtnummer_ist_mehrdeutig_und_wird_nicht_geraten()
    {
        var dienst = Baue(
            new CadastreSchacht("33429", "Schlammsammler", null, null, null, null, 1.0, 2.0),
            new CadastreSchacht("33429", "Einlaufschacht", null, null, null, null, 3.0, 4.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        var mehrdeutig = Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis);
        Assert.Equal(2, mehrdeutig.Kandidaten.Count);
    }

    [Fact]
    public async Task Fehlt_die_Kataster_Datei_nennt_die_Meldung_den_Grund()
    {
        // Kein XTF-Pfad konfiguriert: Der Benutzer soll erfahren, WARUM
        // nichts gefunden wird, statt ein stummes "nicht gefunden" zu sehen.
        var dienst = new KatasterFeldNachschlag(
            new FesterStore(), tabellenPfad: "egal.tsv", xtfPfad: "");

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        var nicht = Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Contains("Abwasserkataster", nicht.Grund, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Einstellungen", nicht.Grund, StringComparison.OrdinalIgnoreCase);
    }
}
```

Damit dieser Test besteht, prueft `SucheAsync` als **erstes**, ob ein
XTF-Pfad konfiguriert und die Datei vorhanden ist:

```csharp
if (string.IsNullOrWhiteSpace(_xtfPfad) || !File.Exists(_xtfPfad))
{
    return Fertig(new FeldNachschlagErgebnis.NichtGefunden(
        "Der Abwasserkataster ist nicht eingerichtet. "
        + "Die XTF-Datei laesst sich in den Einstellungen hinterlegen."));
}
```

- [ ] **Schritt 2: Test laufen lassen, Fehlschlag pruefen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~KatasterFeldNachschlagTests"
```

Erwartet: `CS0246` fuer `KatasterFeldNachschlag`, `FeldNachschlagAnfrage`,
`FeldNachschlagErgebnis`.

- [ ] **Schritt 3: Vertrag und Platzhalter-Regel anlegen**

`src/AuswertungPro.Next.Application/Lookup/FeldNachschlagVertrag.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Was nachgeschlagen werden soll.</summary>
public sealed record FeldNachschlagAnfrage(string Schachtnummer, string Feldname);

/// <summary>Ein gefundener Wert samt seiner Herkunft.</summary>
public sealed record FeldVorschlag(
    string Wert,
    string QuelleKlartext,
    string Herkunftshinweis);

/// <summary>
/// Das Ergebnis eines Nachschlags. Jeder Zustand ist eigenstaendig — ein
/// technischer Fehler darf nie wie "nicht gefunden" aussehen.
/// </summary>
public abstract record FeldNachschlagErgebnis
{
    public sealed record Gefunden(FeldVorschlag Vorschlag) : FeldNachschlagErgebnis;
    public sealed record Mehrdeutig(IReadOnlyList<FeldVorschlag> Kandidaten) : FeldNachschlagErgebnis;
    public sealed record NichtGefunden(string Grund) : FeldNachschlagErgebnis;
    public sealed record Gedrosselt() : FeldNachschlagErgebnis;
    public sealed record Fehler(string Meldung) : FeldNachschlagErgebnis;
}

/// <summary>Eine Quelle, die einen Feldwert liefern kann.</summary>
public interface IFeldWertNachschlag
{
    Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default);
}
```

`src/AuswertungPro.Next.Application/Lookup/KatasterPlatzhalter.cs`:

```csharp
using System;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Der Kataster fuehrt fehlende Angaben als ausgeschriebenen Platzhalter
/// ("unbekannt") oder als Null. Wuerde man sie durchreichen, stuende danach
/// "unbekannt" im Protokoll — schlechter als ein leeres Feld, weil es wie
/// eine gepruefte Aussage aussieht.
/// </summary>
public static class KatasterPlatzhalter
{
    private static readonly string[] Bekannt = ["unbekannt", "unbek.", "unbekannt.", "0", "andere"];

    public static bool IstPlatzhalter(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return true;

        var sauber = wert.Trim();
        foreach (var kandidat in Bekannt)
        {
            if (string.Equals(sauber, kandidat, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
```

- [ ] **Schritt 4: Den Kataster-Anbieter umsetzen**

`src/AuswertungPro.Next.Infrastructure/Lookup/KatasterFeldNachschlag.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Schlaegt Schachtfelder im lokalen Abwasserkataster nach. Rein lesend,
/// ohne Netzzugriff.
/// </summary>
public sealed class KatasterFeldNachschlag : IFeldWertNachschlag
{
    private readonly ISchachtCadastreTableStore _store;
    private readonly string _tabellenPfad;
    private readonly string _xtfPfad;

    public KatasterFeldNachschlag(
        ISchachtCadastreTableStore store, string tabellenPfad, string xtfPfad)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tabellenPfad = tabellenPfad ?? throw new ArgumentNullException(nameof(tabellenPfad));
        _xtfPfad = xtfPfad ?? throw new ArgumentNullException(nameof(xtfPfad));
    }

    public Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        ct.ThrowIfCancellationRequested();

        try
        {
            if (!_store.IsTableFresh(_tabellenPfad, _xtfPfad))
                _store.BuildTable(_xtfPfad, _tabellenPfad);

            var treffer = _store.ReadTable(_tabellenPfad)
                .Where(s => string.Equals(
                    s.Bezeichnung?.Trim(), anfrage.Schachtnummer?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (treffer.Count == 0)
                return Fertig(new FeldNachschlagErgebnis.NichtGefunden(
                    $"Schacht {anfrage.Schachtnummer} steht nicht im Kataster."));

            var vorschlaege = treffer
                .Select(s => LiesFeld(s, anfrage.Feldname))
                .Where(w => !KatasterPlatzhalter.IstPlatzhalter(w))
                .Select(w => new FeldVorschlag(w!, "Abwasserkataster", "Kataster"))
                .ToList();

            if (vorschlaege.Count == 0)
                return Fertig(new FeldNachschlagErgebnis.NichtGefunden(
                    $"Der Kataster fuehrt fuer {anfrage.Feldname} keinen Wert."));

            if (vorschlaege.Count > 1)
                return Fertig(new FeldNachschlagErgebnis.Mehrdeutig(vorschlaege));

            return Fertig(new FeldNachschlagErgebnis.Gefunden(vorschlaege[0]));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Fertig(new FeldNachschlagErgebnis.Fehler(ex.Message));
        }
    }

    /// <summary>Liefert die Lage des Schachts — Grundlage fuer den Grundbuchweg.</summary>
    public (double Ost, double Nord)? LiesLage(string schachtnummer)
    {
        if (!_store.IsTableFresh(_tabellenPfad, _xtfPfad))
            _store.BuildTable(_xtfPfad, _tabellenPfad);

        var treffer = _store.ReadTable(_tabellenPfad)
            .Where(s => string.Equals(
                s.Bezeichnung?.Trim(), schachtnummer?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (treffer.Count != 1)
            return null;

        var s = treffer[0];
        return s.Ost.HasValue && s.Nord.HasValue ? (s.Ost.Value, s.Nord.Value) : null;
    }

    private static string? LiesFeld(CadastreSchacht s, string feldname) => feldname switch
    {
        "Funktion" => s.Funktion,
        "Material" => s.Material,
        _ => null
    };

    private static Task<FeldNachschlagErgebnis> Fertig(FeldNachschlagErgebnis ergebnis)
        => Task.FromResult(ergebnis);
}
```

- [ ] **Schritt 5: Tests laufen lassen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~KatasterFeldNachschlagTests"
```

Erwartet: 8 bestanden (4 Faelle plus 5 Theory-Zeilen minus Ueberschneidung).

- [ ] **Schritt 6: Committen**

```bash
git add src/AuswertungPro.Next.Application/Lookup/ \
        src/AuswertungPro.Next.Infrastructure/Lookup/ \
        tests/AuswertungPro.Next.Infrastructure.Tests/KatasterFeldNachschlagTests.cs
git commit -m "feat(nachschlagen): Kataster-Anbieter mit Platzhalter-Regel

unbekannt, unbek., 0 und andere gelten als kein Wert - sonst stuende
'unbekannt' im Protokoll und saehe aus wie eine gepruefte Aussage.
Doppelte Schachtnummern werden als mehrdeutig gemeldet, nie geraten.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Aufgabe 3: Feldzuordnung und UseCase

**Files:**
- Create: `src/AuswertungPro.Next.Application/Lookup/FeldQuellenTabelle.cs`
- Create: `src/AuswertungPro.Next.Application/UseCases/FeldNachschlagUseCase.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/FeldNachschlagUseCaseTests.cs`

**Interfaces:**
- Consumes: `IFeldWertNachschlag`, `FeldNachschlagAnfrage`,
  `FeldNachschlagErgebnis` aus Aufgabe 2.
- Produces: `FeldQuelle` (Enum: `Kataster`, `Grundbuch`);
  `FeldQuellenTabelle.QuelleFuer(string feldname)` liefert `FeldQuelle?`;
  `FeldQuellenTabelle.UnterstuetzteFelder` als `IReadOnlyList<string>`;
  `FeldNachschlagUseCase.SucheAsync(FeldNachschlagAnfrage, CancellationToken)`.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```csharp
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FeldNachschlagUseCaseTests
{
    private sealed class FesterAnbieter : IFeldWertNachschlag
    {
        private readonly FeldNachschlagErgebnis _ergebnis;
        public int Aufrufe { get; private set; }
        public FesterAnbieter(FeldNachschlagErgebnis ergebnis) => _ergebnis = ergebnis;

        public Task<FeldNachschlagErgebnis> SucheAsync(
            FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
        {
            Aufrufe++;
            return Task.FromResult(_ergebnis);
        }
    }

    [Fact]
    public async Task Funktion_geht_an_den_Kataster_nicht_ans_Grundbuch()
    {
        var kataster = new FesterAnbieter(new FeldNachschlagErgebnis.Gefunden(
            new FeldVorschlag("Schlammsammler", "Abwasserkataster", "Kataster")));
        var grundbuch = new FesterAnbieter(new FeldNachschlagErgebnis.NichtGefunden("x"));
        var useCase = new FeldNachschlagUseCase(kataster, grundbuch);

        var ergebnis = await useCase.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis);
        Assert.Equal(1, kataster.Aufrufe);
        Assert.Equal(0, grundbuch.Aufrufe);
    }

    [Fact]
    public async Task Eigentuemer_geht_ans_Grundbuch_nicht_an_den_Kataster()
    {
        var kataster = new FesterAnbieter(new FeldNachschlagErgebnis.NichtGefunden("x"));
        var grundbuch = new FesterAnbieter(new FeldNachschlagErgebnis.Gefunden(
            new FeldVorschlag("Muster, Hans", "Grundbuch Uri", "Grundbuch")));
        var useCase = new FeldNachschlagUseCase(kataster, grundbuch);

        var ergebnis = await useCase.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis);
        Assert.Equal(0, kataster.Aufrufe);
        Assert.Equal(1, grundbuch.Aufrufe);
    }

    [Fact]
    public async Task Ein_unbekanntes_Feld_wird_gar_nicht_erst_abgefragt()
    {
        var kataster = new FesterAnbieter(new FeldNachschlagErgebnis.NichtGefunden("x"));
        var grundbuch = new FesterAnbieter(new FeldNachschlagErgebnis.NichtGefunden("x"));
        var useCase = new FeldNachschlagUseCase(kataster, grundbuch);

        var ergebnis = await useCase.SucheAsync(new FeldNachschlagAnfrage("33429", "Kosten"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Equal(0, kataster.Aufrufe);
        Assert.Equal(0, grundbuch.Aufrufe);
    }

    [Fact]
    public void Jedes_unterstuetzte_Feld_hat_genau_eine_Quelle()
    {
        foreach (var feld in FeldQuellenTabelle.UnterstuetzteFelder)
            Assert.NotNull(FeldQuellenTabelle.QuelleFuer(feld));

        Assert.Null(FeldQuellenTabelle.QuelleFuer("Kosten"));
        Assert.Null(FeldQuellenTabelle.QuelleFuer("Zustandsklasse"));
    }
}
```

- [ ] **Schritt 2: Test laufen lassen, Fehlschlag pruefen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~FeldNachschlagUseCaseTests"
```

Erwartet: `CS0246` fuer `FeldNachschlagUseCase` und `FeldQuellenTabelle`.

- [ ] **Schritt 3: Feldzuordnung anlegen**

`src/AuswertungPro.Next.Application/Lookup/FeldQuellenTabelle.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Lookup;

public enum FeldQuelle
{
    Kataster,
    Grundbuch
}

/// <summary>
/// Welches Schachtfeld aus welcher Quelle kommt. Bewusst eine Tabelle und
/// keine Verzweigung im UseCase: So bleibt die Zuordnung testbar und laesst
/// sich ohne Aenderung an der Oberflaeche erweitern.
/// </summary>
public static class FeldQuellenTabelle
{
    private static readonly Dictionary<string, FeldQuelle> Zuordnung =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Funktion"] = FeldQuelle.Kataster,
            ["Material"] = FeldQuelle.Kataster,
            ["Eigentuemer"] = FeldQuelle.Grundbuch,
            ["Eigentümer"] = FeldQuelle.Grundbuch,
            ["Strasse"] = FeldQuelle.Grundbuch,
        };

    public static IReadOnlyList<string> UnterstuetzteFelder =>
        Zuordnung.Keys.ToList();

    public static FeldQuelle? QuelleFuer(string? feldname)
        => feldname is not null && Zuordnung.TryGetValue(feldname.Trim(), out var quelle)
            ? quelle
            : null;
}
```

Hinweis: `Eigentuemer` und `Eigentümer` stehen beide drin, weil in den echten
Projekten beide Schreibweisen vorkommen.

- [ ] **Schritt 4: Den UseCase umsetzen**

`src/AuswertungPro.Next.Application/UseCases/FeldNachschlagUseCase.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>
/// Waehlt anhand des Feldnamens die zustaendige Quelle und reicht deren
/// Ergebnis unveraendert weiter. Schreibt selbst nichts.
/// </summary>
public sealed class FeldNachschlagUseCase
{
    private readonly IFeldWertNachschlag _kataster;
    private readonly IFeldWertNachschlag _grundbuch;

    public FeldNachschlagUseCase(IFeldWertNachschlag kataster, IFeldWertNachschlag grundbuch)
    {
        _kataster = kataster ?? throw new ArgumentNullException(nameof(kataster));
        _grundbuch = grundbuch ?? throw new ArgumentNullException(nameof(grundbuch));
    }

    public Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);

        var quelle = FeldQuellenTabelle.QuelleFuer(anfrage.Feldname);
        if (quelle is null)
        {
            return Task.FromResult<FeldNachschlagErgebnis>(
                new FeldNachschlagErgebnis.NichtGefunden(
                    $"Fuer das Feld {anfrage.Feldname} gibt es keine Quelle."));
        }

        return quelle == FeldQuelle.Kataster
            ? _kataster.SucheAsync(anfrage, ct)
            : _grundbuch.SucheAsync(anfrage, ct);
    }
}
```

- [ ] **Schritt 5: Tests laufen lassen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~FeldNachschlagUseCaseTests"
```

Erwartet: 4 bestanden.

- [ ] **Schritt 6: Committen**

```bash
git add src/AuswertungPro.Next.Application/Lookup/FeldQuellenTabelle.cs \
        src/AuswertungPro.Next.Application/UseCases/FeldNachschlagUseCase.cs \
        tests/AuswertungPro.Next.Infrastructure.Tests/FeldNachschlagUseCaseTests.cs
git commit -m "feat(nachschlagen): Feldzuordnung und UseCase

Die Feld-zu-Quelle-Tabelle liegt getrennt vom UseCase, damit sie testbar
bleibt. Ein Feld ohne Quelle wird gar nicht erst abgefragt.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Aufgabe 4: Herkunft und der Merge-Schutz

Diese Aufgabe ist klein, aber sie ist die wichtigste des ganzen Plans: Sie
haelt fest, woran der Schutz nachgeschlagener Werte wirklich haengt.

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Models/FieldSource.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/NachgeschlagenerWertMergeSchutzTests.cs`

**Interfaces:**
- Consumes: nichts aus frueheren Aufgaben.
- Produces: `FieldSource.Kataster` und `FieldSource.Grundbuch`.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```csharp
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Schutz eines nachgeschlagenen Werts haengt AUSSCHLIESSLICH an
/// userEdited: true. Eine niedrige Merge-Prioritaet schuetzt NICHT — die
/// MergeEngine entscheidet zugunsten der HOEHEREN Zahl, und neue Herkuenfte
/// bekommen ueber den Fall-through die 0. Beide Tests zusammen belegen das.
/// </summary>
public sealed class NachgeschlagenerWertMergeSchutzTests
{
    [Fact]
    public void Die_neuen_Herkuenfte_existieren()
    {
        Assert.True(Enum.IsDefined(typeof(FieldSource), FieldSource.Kataster));
        Assert.True(Enum.IsDefined(typeof(FieldSource), FieldSource.Grundbuch));
    }

    [Fact]
    public void Mit_userEdited_ueberlebt_der_Wert_einen_Import()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: true);

        // Ein automatischer Importschreibvorgang darf einen Handwert nicht anfassen.
        var ergebnis = schacht.SetFieldValue("Funktion", "Etwas anderes", FieldSource.Xtf, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.HandwertGeschuetzt, ergebnis);
        Assert.Equal("Schlammsammler", schacht.GetFieldValue("Funktion"));
    }

    [Fact]
    public void Ohne_userEdited_ist_derselbe_Wert_ungeschuetzt()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: false);

        schacht.SetFieldValue("Funktion", "Etwas anderes", FieldSource.Xtf, userEdited: false);

        // Genau deshalb ist userEdited: true beim Nachschlagen Pflicht.
        Assert.Equal("Etwas anderes", schacht.GetFieldValue("Funktion"));
    }
}
```

- [ ] **Schritt 2: Test laufen lassen, Fehlschlag pruefen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~NachgeschlagenerWertMergeSchutzTests"
```

Erwartet: `CS0117: "FieldSource" enthaelt keine Definition fuer "Kataster"`.

- [ ] **Schritt 3: Die zwei Herkuenfte ergaenzen**

In `src/AuswertungPro.Next.Domain/Models/FieldSource.cs` hinter `Manual`
einfuegen:

```csharp
    /// <summary>Aus dem amtlichen Abwasserkataster nachgeschlagen und bestaetigt.</summary>
    Kataster,

    /// <summary>Aus dem Grundbuch nachgeschlagen und bestaetigt.</summary>
    Grundbuch,
```

`MergeEngine` wird **nicht** angefasst. Beide Werte laufen dort in den
Fall-through `_ => 0`; das ist richtig so, weil der Schutz ueber `userEdited`
laeuft und nicht ueber die Prioritaet.

- [ ] **Schritt 4: Tests laufen lassen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~NachgeschlagenerWertMergeSchutzTests"
```

Erwartet: 3 bestanden.

- [ ] **Schritt 5: Volle Testsuite, weil ein Enum erweitert wurde**

```bash
dotnet test AuswertungPro.sln
```

Erwartet: vollstaendig gruen. Ein `switch` ueber `FieldSource` ohne
Standardfall wuerde hier auffallen.

- [ ] **Schritt 6: Committen**

```bash
git add src/AuswertungPro.Next.Domain/Models/FieldSource.cs \
        tests/AuswertungPro.Next.Infrastructure.Tests/NachgeschlagenerWertMergeSchutzTests.cs
git commit -m "feat(nachschlagen): Herkuenfte Kataster und Grundbuch

Der Gegentest haelt fest, dass der Schutz allein an userEdited haengt:
Ohne das Flag wird derselbe Wert vom naechsten Import ueberschrieben.
Eine niedrige Merge-Prioritaet schuetzt nicht.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Aufgabe 5: Kontextmenue und Vorschlagsfenster

**Vor dem Beginn:** `git status` pruefen. An `SchachtansichtView.xaml(.cs)`
und `RecordDetailsView` wurde parallel gearbeitet.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsModels.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml`
- Modify: `src/AuswertungPro.Next.UI/DataPage/SchaechteRecordDetailsBuilder.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Windows/FeldVorschlagWindow.xaml(.cs)`
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`,
  `ServiceProviderRegistrationMap.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/FeldNachschlagMenueTests.cs`

**Interfaces:**
- Consumes: `FeldNachschlagUseCase`, `FeldQuellenTabelle`,
  `FeldNachschlagErgebnis` aus den Aufgaben 2 und 3.
- Produces: `RecordDetailItem.NachschlagenCommand` (ICommand) und
  `RecordDetailItem.KannNachschlagen` (bool).

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```csharp
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FeldNachschlagMenueTests
{
    [Fact]
    public void Das_Kontextmenue_bietet_den_Nachschlag_an()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Controls", "RecordDetailsView.xaml"));

        Assert.Contains("Beim Kanton nachschlagen", xaml, StringComparison.Ordinal);
        Assert.Contains("NachschlagenCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Menuepunkt_haengt_an_KannNachschlagen()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Controls", "RecordDetailsView.xaml"));

        // Ohne diese Bindung erschiene der Punkt auch an gefuellten Feldern.
        Assert.Contains("KannNachschlagen", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Es_gibt_keinen_Sammellauf()
    {
        var quellen = Directory.GetFiles(
            RepoDirectory("src", "AuswertungPro.Next.UI"), "*.*", SearchOption.AllDirectories);

        foreach (var datei in quellen)
        {
            if (!datei.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                && !datei.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                continue;
            if (datei.Contains(Path.Combine("obj", ""), StringComparison.OrdinalIgnoreCase)
                || datei.Contains(Path.Combine("bin", ""), StringComparison.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(datei);
            Assert.DoesNotContain("AlleFelderNachschlagen", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

Hinweis: `RepoDirectory` gegebenenfalls analog `RepoFile` in
`TestRepoPaths` ergaenzen.

- [ ] **Schritt 2: Test laufen lassen, Fehlschlag pruefen**

```bash
dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~FeldNachschlagMenueTests"
```

Erwartet: Die ersten zwei Tests schlagen fehl ("Beim Kanton nachschlagen"
nicht gefunden), der dritte besteht bereits.

- [ ] **Schritt 3: `RecordDetailItem` erweitern**

In `src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsModels.cs` den
Konstruktor um einen weiteren optionalen Parameter ergaenzen (hinter
`removeOptionCommand`) und zwei Eigenschaften anlegen:

```csharp
        ICommand? nachschlagenCommand = null,
```

```csharp
    public ICommand? NachschlagenCommand { get; }

    /// <summary>
    /// Nur wenn das Feld leer ist UND eine Quelle kennt. Ein gefuelltes Feld
    /// darf nicht versehentlich ueberschrieben werden.
    /// </summary>
    public bool KannNachschlagen
        => NachschlagenCommand is not null
           && string.IsNullOrWhiteSpace(Value)
           && FeldQuellenTabelle.QuelleFuer(FieldName) is not null;
```

Im Konstruktorrumpf: `NachschlagenCommand = nachschlagenCommand;`

- [ ] **Schritt 4: Menuepunkt im XAML ergaenzen**

In `src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml` ein
eigenes Kontextmenue fuer Textfelder anlegen (neben dem bestehenden
`ManagedOptionsContextMenu`):

```xml
        <ContextMenu x:Key="NachschlagContextMenu">
            <MenuItem Header="Beim Kanton nachschlagen"
                      Command="{Binding PlacementTarget.DataContext.NachschlagenCommand,
                                RelativeSource={RelativeSource AncestorType=ContextMenu}}">
                <MenuItem.Icon>
                    <ui:FluentIcon Glyph="&#xE721;" Foreground="{DynamicResource AccentBrush}"/>
                </MenuItem.Icon>
                <MenuItem.Style>
                    <Style TargetType="MenuItem" BasedOn="{StaticResource {x:Type MenuItem}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding PlacementTarget.DataContext.KannNachschlagen,
                                         RelativeSource={RelativeSource AncestorType=ContextMenu}}"
                                         Value="False">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </MenuItem.Style>
            </MenuItem>
        </ContextMenu>
```

Und im `TextEditorTemplate` an der `TextBox`:
`ContextMenu="{StaticResource NachschlagContextMenu}"`.

- [ ] **Schritt 5: Das Vorschlagsfenster anlegen**

`src/AuswertungPro.Next.UI/Views/Windows/FeldVorschlagWindow.xaml` — ein
schmales Fenster im bestehenden Themenstil mit:

- Kopfzeile `Schacht {Nummer} · Feld "{Feldname}"`
- Bereich `Gefunden:` mit dem Wert (bei mehreren Kandidaten eine `ListBox`
  mit Einfachauswahl)
- Zeile `Quelle:` mit `QuelleKlartext`
- Knoepfe `Uebernehmen` (nur aktiv bei getroffener Auswahl) und `Abbrechen`

Bei `NichtGefunden`, `Gedrosselt` und `Fehler` zeigt dasselbe Fenster den
jeweiligen Klartext und nur `Schliessen` — nie einen leeren Vorschlag.

- [ ] **Schritt 6: Command im Schacht-Builder verdrahten**

In `src/AuswertungPro.Next.UI/DataPage/SchaechteRecordDetailsBuilder.cs` beim
Erzeugen jedes `RecordDetailItem` das neue Command mitgeben. Es ruft
`FeldNachschlagUseCase.SucheAsync`, oeffnet `FeldVorschlagWindow` und
schreibt nur bei Bestaetigung:

```csharp
schacht.SetFieldValue(feldname, gewaehlt.Wert, herkunft, userEdited: true);
```

wobei `herkunft` aus `FeldVorschlag.Herkunftshinweis` abgeleitet wird
(`"Kataster"` → `FieldSource.Kataster`, `"Grundbuch"` → `FieldSource.Grundbuch`).

**`userEdited: true` ist Pflicht** — siehe Aufgabe 4.

- [ ] **Schritt 7: Dienste registrieren**

In `ServiceProvider.cs` eine Eigenschaft
`public FeldNachschlagUseCase FeldNachschlag { get; }` anlegen und im
Konstruktor bauen; in `ServiceProviderRegistrationMap.cs` eintragen. Die
erwartete Registrierungszahl in `ServiceProviderRegistrationTests` um die
tatsaechlich ergaenzte Anzahl erhoehen — mit einer neuen Begruendungszeile in
der bestehenden Historie.

- [ ] **Schritt 8: Bauen und testen**

**SewerStudio muss geschlossen sein.**

```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

Erwartet: 0 Fehler, 0 neue Warnungen, alle Tests gruen.

- [ ] **Schritt 9: Sichtpruefung im Programm**

Projekt `Jagdmatt_Erstfeld_2026` oeffnen, Seite `Schaechte`, einen Schacht
waehlen. Rechtsklick auf das leere Feld `Funktion` zeigt "Beim Kanton
nachschlagen". Der Klick liefert einen Vorschlag; `Uebernehmen` schreibt ihn.
Ein Rechtsklick auf ein gefuelltes Feld zeigt den Punkt **nicht**.

- [ ] **Schritt 10: Committen**

```bash
git add src/AuswertungPro.Next.UI tests/AuswertungPro.Next.UI.Tests
git commit -m "feat(nachschlagen): Rechtsklick und Vorschlagsfenster

Der Menuepunkt erscheint nur an leeren Feldern mit bekannter Quelle.
Uebernommen wird erst nach Bestaetigung, mit userEdited: true.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## HALT — hier ausprobieren

Stufe 1 ist fertig und im Programm nutzbar. **Vor Stufe 2 ausprobieren**, ob
die Bedienung passt: Erscheint der Menuepunkt an den richtigen Stellen? Ist
das Vorschlagsfenster verstaendlich? Stimmt der Ablauf?

Aenderungen an der Bedienung sind jetzt billig, weil erst eine harmlose
lokale Quelle daranhaengt. Ab Stufe 2 haengt eine Quelle mit echten
Personendaten daran.

---

## Aufgabe 6: Der Grundbuch-Anbieter

**Files:**
- Create: `src/AuswertungPro.Next.Application/Lookup/PunktAlsKurzeLinie.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Lookup/GrundbuchFeldNachschlag.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/GrundbuchFeldNachschlagTests.cs`

**Interfaces:**
- Consumes: `IFeldWertNachschlag`, `FeldNachschlagErgebnis` (Aufgabe 2);
  `KatasterFeldNachschlag.LiesLage(string)` (Aufgabe 2);
  `IParcelLookup.FindTouchedAsync(IReadOnlyList<string> wktLines, CancellationToken)`
  und `ILandRegistryLookup.ReadAsync(ParcelInfo, CancellationToken)`
  (bestehend).
- Produces: `PunktAlsKurzeLinie.Baue(double ost, double nord, double halbeLaenge = 0.5)`
  liefert `string` im Format `(x1 y1, x2 y2)`.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GrundbuchFeldNachschlagTests
{
    [Fact]
    public void Aus_einem_Punkt_wird_eine_kurze_Linie()
    {
        var wkt = PunktAlsKurzeLinie.Baue(2692606.892, 1192380.717);

        // Eine Linie von einem Meter Laenge um den Punkt herum.
        Assert.Contains("2692606.392", wkt);
        Assert.Contains("2692607.392", wkt);
    }

    private sealed class FesteParzelle : IParcelLookup
    {
        private readonly IReadOnlyList<ParcelInfo> _treffer;
        public FesteParzelle(params ParcelInfo[] treffer) => _treffer = treffer;

        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => Task.FromResult<ParcelInfo?>(null);
        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
            IReadOnlyList<string> wktLines, CancellationToken ct = default)
            => Task.FromResult(_treffer);
        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Municipality>>([]);
    }

    private sealed class FestesGrundbuch : ILandRegistryLookup
    {
        private readonly LandRegistryEntry? _eintrag;
        public FestesGrundbuch(LandRegistryEntry? eintrag) => _eintrag = eintrag;
        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult(_eintrag);
    }

    private static ParcelInfo Parzelle(string nummer)
        => new(nummer, 1210, "Erstfeld", 500, "CH1", "POLYGON((0 0))", "https://example.invalid");

    [Fact]
    public async Task Mehrere_Eigentuemer_werden_zur_Auswahl_gestellt()
    {
        var eintrag = new LandRegistryEntry("Musterweg", "4", "6472", "Erstfeld",
            [new LandRegistryOwner("Lit.A", "Muster, Hans", "Musterweg 4", "1/2"),
             new LandRegistryOwner("Lit.B", "Beispiel, Anna", "Musterweg 4", "1/2")],
            NoOwnerRegistered: false);

        var dienst = new GrundbuchFeldNachschlag(
            _ => (2692606.892, 1192380.717),
            new FesteParzelle(Parzelle("439")),
            new FestesGrundbuch(eintrag));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        var mehrdeutig = Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis);
        Assert.Equal(2, mehrdeutig.Kandidaten.Count);
    }

    [Fact]
    public async Task Mehrere_Parzellen_werden_nicht_geraten()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => (2692606.892, 1192380.717),
            new FesteParzelle(Parzelle("439"), Parzelle("440")),
            new FestesGrundbuch(null));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis);
    }

    [Fact]
    public async Task Ohne_Lage_gibt_es_keine_Abfrage()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => null,
            new FesteParzelle(Parzelle("439")),
            new FestesGrundbuch(null));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("99999", "Eigentuemer"));

        var nicht = Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Contains("Kataster", nicht.Grund, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eine_Drosselung_ist_ein_eigener_Zustand()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => (1.0, 2.0),
            new WirftDrosselung(),
            new FestesGrundbuch(null));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.Gedrosselt>(ergebnis);
    }

    private sealed class WirftDrosselung : IParcelLookup
    {
        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
            IReadOnlyList<string> wktLines, CancellationToken ct = default)
            => throw new GeoUrRequestFailedException("HTTP 429");
        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
```

Hinweis: `GeoUrRequestFailedException` liegt in
`AuswertungPro.Next.Infrastructure.Dossiers.Lookup`. Vor der Umsetzung dort
nachsehen, wie eine Drosselung tatsaechlich signalisiert wird, und den Test
daran angleichen.

- [ ] **Schritt 2: Test laufen lassen, Fehlschlag pruefen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~GrundbuchFeldNachschlagTests"
```

Erwartet: `CS0246` fuer `GrundbuchFeldNachschlag` und `PunktAlsKurzeLinie`.

- [ ] **Schritt 3: Punkt-zu-Linie umsetzen**

`src/AuswertungPro.Next.Application/Lookup/PunktAlsKurzeLinie.cs`:

```csharp
using System.Globalization;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Der Parzellendienst sucht mit Linien (fuer Haltungen gebaut). Ein Schacht
/// ist ein Punkt. Statt den bewaehrten WFS-Client zu aendern, wird aus dem
/// Punkt eine sehr kurze Linie gebaut.
/// </summary>
public static class PunktAlsKurzeLinie
{
    public static string Baue(double ost, double nord, double halbeLaenge = 0.5)
    {
        var links = (ost - halbeLaenge).ToString("0.###", CultureInfo.InvariantCulture);
        var rechts = (ost + halbeLaenge).ToString("0.###", CultureInfo.InvariantCulture);
        var y = nord.ToString("0.###", CultureInfo.InvariantCulture);
        return $"({links} {y}, {rechts} {y})";
    }
}
```

- [ ] **Schritt 4: Den Grundbuch-Anbieter umsetzen**

`src/AuswertungPro.Next.Infrastructure/Lookup/GrundbuchFeldNachschlag.cs`.
Er bekommt die Lagequelle als Delegat, damit er im Test ohne Kataster
auskommt:

```csharp
public sealed class GrundbuchFeldNachschlag : IFeldWertNachschlag
{
    private readonly Func<string, (double Ost, double Nord)?> _lageQuelle;
    private readonly IParcelLookup _parzellen;
    private readonly ILandRegistryLookup _grundbuch;
    private readonly Action<string>? _log;

    public GrundbuchFeldNachschlag(
        Func<string, (double Ost, double Nord)?> lageQuelle,
        IParcelLookup parzellen,
        ILandRegistryLookup grundbuch,
        Action<string>? log = null)
    {
        _lageQuelle = lageQuelle ?? throw new ArgumentNullException(nameof(lageQuelle));
        _parzellen = parzellen ?? throw new ArgumentNullException(nameof(parzellen));
        _grundbuch = grundbuch ?? throw new ArgumentNullException(nameof(grundbuch));
        _log = log;
    }
```

Der Log-Delegat ist optional, damit die Tests ohne Protokoll auskommen.

```csharp
public async Task<FeldNachschlagErgebnis> SucheAsync(
    FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(anfrage);

    try
    {
        var lage = _lageQuelle(anfrage.Schachtnummer);
        if (lage is null)
        {
            return new FeldNachschlagErgebnis.NichtGefunden(
                "Der Schacht steht nicht mit Lage im Kataster. "
                + "Ohne Lage laesst sich die Parzelle nicht bestimmen.");
        }

        var linie = PunktAlsKurzeLinie.Baue(lage.Value.Ost, lage.Value.Nord);
        var parzellen = await _parzellen.FindTouchedAsync([linie], ct).ConfigureAwait(false);

        if (parzellen.Count == 0)
        {
            return new FeldNachschlagErgebnis.NichtGefunden(
                "An dieser Lage liegt keine Parzelle des Kantons Uri.");
        }

        if (parzellen.Count > 1)
        {
            // Der Schacht liegt auf einer Grenze. Nicht raten - fragen.
            return new FeldNachschlagErgebnis.Mehrdeutig(parzellen
                .Select(p => new FeldVorschlag(
                    p.Number, $"Grundbuch Uri, Parzelle {p.Number} ({p.Municipality})", "Grundbuch"))
                .ToList());
        }

        var parzelle = parzellen[0];
        var eintrag = await _grundbuch.ReadAsync(parzelle, ct).ConfigureAwait(false);

        if (eintrag is null || eintrag.NoOwnerRegistered)
        {
            return new FeldNachschlagErgebnis.NichtGefunden(
                $"Fuer Parzelle {parzelle.Number} ist kein Eigentuemer eingetragen.");
        }

        var quelle = $"Grundbuch Uri, Parzelle {parzelle.Number} ({parzelle.Municipality})";

        if (IstEigentuemerfeld(anfrage.Feldname))
        {
            var namen = eintrag.Owners
                .Where(o => !string.IsNullOrWhiteSpace(o.Name))
                .Select(o => new FeldVorschlag(o.Name.Trim(), quelle, "Grundbuch"))
                .ToList();

            if (namen.Count == 0)
                return new FeldNachschlagErgebnis.NichtGefunden("Kein Eigentuemername vorhanden.");

            // Miteigentum und Stockwerkeigentum: alle zur Auswahl stellen.
            return namen.Count == 1
                ? new FeldNachschlagErgebnis.Gefunden(namen[0])
                : new FeldNachschlagErgebnis.Mehrdeutig(namen);
        }

        var strasse = string.Join(' ', new[]
            {
                eintrag.BuildingStreet?.Trim(),
                eintrag.BuildingHouseNumber?.Trim()
            }
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        return string.IsNullOrWhiteSpace(strasse)
            ? new FeldNachschlagErgebnis.NichtGefunden("Keine Gebaeudeadresse eingetragen.")
            : new FeldNachschlagErgebnis.Gefunden(
                new FeldVorschlag(strasse, quelle, "Grundbuch"));
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex) when (IstDrosselung(ex))
    {
        // Nur die Fehlerklasse ins Protokoll - nie Namen oder Adressen.
        _log?.Invoke("Grundbuchabfrage gedrosselt.");
        return new FeldNachschlagErgebnis.Gedrosselt();
    }
    catch (Exception ex)
    {
        _log?.Invoke($"Grundbuchabfrage fehlgeschlagen: {ex.GetType().Name}");
        return new FeldNachschlagErgebnis.Fehler(ex.Message);
    }
}

private static bool IstEigentuemerfeld(string feldname)
    => feldname.StartsWith("Eigent", StringComparison.OrdinalIgnoreCase);

private static bool IstDrosselung(Exception ex)
    => ex.Message.Contains("429", StringComparison.Ordinal)
       || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
```

**Vor der Umsetzung nachsehen**, wie `GeoUrHttpGateway` eine Drosselung
tatsaechlich meldet — wirft es `GeoUrRequestFailedException` mit "429" im
Text, oder gibt es einen eigenen Statuscode? `IstDrosselung` entsprechend
angleichen und den Test aus Schritt 1 daran anpassen.

**Protokollierung:** Wie oben zu sehen, geht nur die Fehlerklasse ins Log —
niemals ein Name, niemals eine Adresse. Aufgabe 7 sichert das mit einem
Waechter ab.

- [ ] **Schritt 5: Tests laufen lassen**

```bash
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "FullyQualifiedName~GrundbuchFeldNachschlagTests"
```

Erwartet: 5 bestanden.

- [ ] **Schritt 6: Committen**

```bash
git add src/AuswertungPro.Next.Application/Lookup/PunktAlsKurzeLinie.cs \
        src/AuswertungPro.Next.Infrastructure/Lookup/GrundbuchFeldNachschlag.cs \
        tests/AuswertungPro.Next.Infrastructure.Tests/GrundbuchFeldNachschlagTests.cs
git commit -m "feat(nachschlagen): Grundbuch-Anbieter ueber die Kataster-Lage

Raeumliche Suche statt Parzellennummer - damit entfaellt die Gemeinde-Falle.
Mehrere Parzellen oder Eigentuemer werden zur Auswahl gestellt, nie geraten.
Eine Drosselung ist ein eigener Zustand, kein 'nicht gefunden'.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Aufgabe 7: Grundbuch anschliessen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/FeldNachschlagLogTests.cs`

**Interfaces:**
- Consumes: `GrundbuchFeldNachschlag` (Aufgabe 6), `FeldNachschlagUseCase`
  (Aufgabe 3), `KatasterFeldNachschlag.LiesLage` (Aufgabe 2).
- Produces: nichts Neues.

- [ ] **Schritt 1: Den Datenschutztest schreiben**

```csharp
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FeldNachschlagLogTests
{
    [Fact]
    public void Der_Grundbuchweg_protokolliert_keine_Personendaten()
    {
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Lookup", "GrundbuchFeldNachschlag.cs"));

        var logzeilen = quelle.Split('\n')
            .Where(z => z.Contains("Log", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var zeile in logzeilen)
        {
            Assert.DoesNotContain(".Name", zeile, StringComparison.Ordinal);
            Assert.DoesNotContain("AddressLine", zeile, StringComparison.Ordinal);
            Assert.DoesNotContain("Owners", zeile, StringComparison.Ordinal);
        }
    }
}
```

- [ ] **Schritt 2: Test laufen lassen**

```bash
dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~FeldNachschlagLogTests"
```

Erwartet: besteht (der Anbieter aus Aufgabe 6 protokolliert bereits richtig).
Schlaegt er fehl, ist eine Logzeile in Aufgabe 6 zu korrigieren.

- [ ] **Schritt 3: Beide Anbieter im ServiceProvider verbinden**

Den `FeldNachschlagUseCase` aus Aufgabe 5 so umbauen, dass er statt zweimal
demselben Kataster-Anbieter den echten `GrundbuchFeldNachschlag` erhaelt. Die
Lagequelle ist `katasterAnbieter.LiesLage`.

- [ ] **Schritt 4: Bauen und volle Suite**

```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

- [ ] **Schritt 5: Sichtpruefung mit echten Daten**

Rechtsklick auf ein leeres `Eigentuemer`-Feld eines Schachts. Der Vorschlag
muss denselben Eigentuemer nennen, den das Eigentuemerdossier fuer diese
Liegenschaft anzeigt. **Diesen Abgleich unbedingt an mindestens zwei
Schaechten machen** — eine falsche Parzellenzuordnung ist der teuerste
denkbare Fehler dieses Features.

- [ ] **Schritt 6: Committen**

```bash
git add src/AuswertungPro.Next.UI tests/AuswertungPro.Next.UI.Tests
git commit -m "feat(nachschlagen): Grundbuch angeschlossen

Der UseCase leitet Eigentuemer und Strasse an das Grundbuch, Funktion und
Material an den Kataster. Ein Waechter haelt fest, dass keine Namen ins
Protokoll gelangen.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Beweis

Der Umbau gilt erst als belegt, wenn alles zutrifft:

- [ ] `dotnet build AuswertungPro.sln` ohne Fehler und ohne neue Warnungen
- [ ] `dotnet test AuswertungPro.sln` vollstaendig gruen
- [ ] Rechtsklick auf leeres `Funktion` eines Schachts aus
      `Jagdmatt_Erstfeld_2026` liefert einen Kataster-Vorschlag
- [ ] Rechtsklick auf leeres `Eigentuemer` liefert denselben Eigentuemer wie
      das Eigentuemerdossier — an mindestens zwei Schaechten geprueft
- [ ] Ein gefuelltes Feld bietet den Menuepunkt nicht an
- [ ] Nach dem Uebernehmen steht die neue Herkunft in den Feldmetadaten
- [ ] Ein anschliessender Import ueberschreibt den uebernommenen Wert nicht
- [ ] Die Logdatei enthaelt keinen Eigentuemernamen
