# Vier Arbeitspakete aus dem Architektur-Audit — 2026-08-10

Grundlage ist das Audit vom 2026-08-10: acht Dimensionen geprueft, 42 Befunde
gemeldet, 18 haben die adversarische Gegenprobe ueberstanden. Kein Befund behielt
die Schwere „hoch". Von den 18 kommen hier die vier, bei denen echter Schaden
entsteht. Der Rest ist Kosmetik und wird bewusst nicht beauftragt.

## Vor dem Anfangen — fuer beide Bearbeiter

**Aufteilung.** Jede Datei gehoert genau einem Paket. Es gibt keine
Ueberschneidung; die vier Pakete koennen gleichzeitig laufen. Wer ein Paket
beginnt, sagt es an, damit nicht zwei am selben Bestand arbeiten. Am 2026-08-09
ist genau das zweimal schiefgegangen: ein Release-Build brach mitten im Lauf,
weil eine Datei gerade von der anderen Seite bearbeitet wurde.

**Kein Paket gilt als fertig ohne seine Messung.** Jedes Paket nennt unten eine
Pruefung, die vorher fehlschlaegt und nachher haelt. Eine plausible Aenderung
ohne Gegenprobe kann genauso gut das Gegenteil bewirken — am 2026-08-09 dreimal
passiert.

**Reihenfolge bei Zeitdruck:** AP-1, dann AP-3, dann AP-2, dann AP-4.
AP-1 schuetzt Monate persoenlicher Handarbeit.

**Hausregeln gelten unveraendert:** `CLAUDE.md` lesen. Geschaeftslogik in C#,
Dienste ueber den ServiceProvider, Kommentare auf Deutsch, kein grosses
Refactoring am Bestand ohne Ruecksprache. Build `dotnet build AuswertungPro.sln`,
Tests `dotnet test AuswertungPro.sln`.

---

## AP-1 — Ein Lesefehler an der Golddatei darf nicht zum Loeschen fuehren

**Schwere: die hoechste der vier. Betrifft persoenliche Handlabels.**

**Datei:** `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSampleFileStore.cs`
**Stellen:** Zeile 345 (`catch`), Zeile 386 (`return [];`), Aufrufer Zeile 114,
168 und 204.

### Was heute passiert

`LoadInternalAsync` faengt in Zeile 345 jede Ausnahme:

```csharp
catch (Exception ex)
{
    return await RecoverFromBackupAsync(path, ex).ConfigureAwait(false);
}
```

Sind Hauptdatei und alle Sicherungskopien im selben Moment nicht lesbar, endet
die Wiederherstellung in Zeile 386:

```csharp
BestEffort.ReportWarning(
    "[TrainingSampleFileStore] KRITISCH: Kein lesbares Backup gefunden, starte leer");
return [];
```

Die Aufrufer behandeln diese leere Liste nicht als Fehler. `MergeOrUpdateAsync`
und `MergeAndSaveAsync` rechnen mit `existing = []` weiter und rufen
`SaveInternalAsync(existing)` auf. Danach steht in `training_samples.json` nur
noch das eine gerade bestaetigte Sample.

**Wie es ausgeloest wird:** Es braucht keinen Defekt. Eine voruebergehende
Dateisperre genuegt — durch `KnowledgeRealtimeMirrorService`, einen
Virenscanner oder eine zweite laufende Instanz.

**Warum es niemand merkt:** Es erscheint keine Fehlermeldung. Das Speichern
meldet Erfolg. Die Altdaten liegen noch in `.bak`, rotieren aber bei jedem
weiteren Speichern eine Stufe weiter (`RotateBackups`, Zeile 481) und sind nach
drei weiteren Speichervorgaengen verschwunden.

### Die Aenderung

Zwischen zwei Faellen unterscheiden, die heute gleich behandelt werden:

| Fall | heute | kuenftig |
|---|---|---|
| Datei existiert nicht | leere Liste | leere Liste (unveraendert, das ist der Erstlauf) |
| Datei existiert, ist aber nicht lesbar | leere Liste | **Ausnahme werfen** |

Wenn weder die Hauptdatei noch eine Sicherungskopie gelesen werden kann, muss
`LoadInternalAsync` werfen statt `[]` zu liefern. Dadurch brechen
`MergeOrUpdateAsync`, `MergeAndSaveAsync` und `SaveAsync` ab, und der Benutzer
sieht die vorhandene Meldung „Training nicht gespeichert".

Ein Schreibvorgang auf Grundlage eines gescheiterten Lesevorgangs darf nicht
stattfinden. Das ist dieselbe Regel, die im Projekt fuer die Kostendateien schon
gilt: `CostStoreFileProbe` unterscheidet fehlende Dateien von unlesbaren, und
bei einem Lesefehler wird nichts ueberschrieben. Diese Unterscheidung fehlt hier.

**Nicht anfassen:** die Backup-Rotation selbst, das Wiederherstellen aus einer
lesbaren Kopie, den Erstlauf ohne Datei.

### Pflichttests

1. Hauptdatei vorhanden aber gesperrt, alle Sicherungskopien gesperrt, dann
   `MergeOrUpdateAsync` aufrufen: Es muss eine Ausnahme kommen, **und die
   Golddatei muss danach byte-gleich sein wie vorher.** Der zweite Teil ist der
   wichtigere.
2. Keine Datei vorhanden: leere Liste, kein Fehler — bisheriges Verhalten bleibt.
3. Hauptdatei defekt, eine Sicherungskopie lesbar: Wiederherstellung wie bisher.

### Messung, die Erfolg belegt

Test 1 schlaegt vor der Aenderung fehl (die Datei wird ueberschrieben) und haelt
danach. Volle Testsuite bleibt gruen.

---

## AP-2 — Der Kachelregler darf nicht die ganze Einstellungsdatei ueberschreiben

**Datei:** `src/AuswertungPro.Next.UI/Controls/PhotoGalleryPanel.xaml.cs`
**Stellen:** Zeile 35 (Konstruktor), Zeile 109-110 (Speichern).

### Was heute passiert

```csharp
public PhotoGalleryPanel()
{
    _settings = AppSettings.Load();
```

Das Bedienelement laedt eine **zweite, eigene** Einstellungskopie von der Platte.
`ViewCustomizationStore` verbietet das ausdruecklich im Code: „WICHTIG: Niemals
eine eigene AppSettings.Load()-Instanz erzeugen". Die eine Live-Instanz wird in
`App.xaml.cs:90-91` bereitgestellt.

Beim Ziehen des Reglers:

```csharp
panel._settings.PhotoGalleryTileSize = clamped;
panel._settings.Save();
```

`AppSettings.Save()` schreibt immer das **ganze** Objekt und ersetzt die
komplette `settings.json`.

**Folge:** Der Benutzer oeffnet die Haltungsansicht — dabei entsteht die
Momentaufnahme. Er aendert danach irgendetwas anderes in der App: zuletzt
geoeffnetes Projekt, Verteil- oder Exportpfade, KnowledgeRoot, Fensterlagen,
Spaltenlayouts, KI-Einstellungen. Dann zieht er am Kachelregler. Geschrieben
wird die veraltete Momentaufnahme; alle Aenderungen seit dem Oeffnen der Seite
sind weg. Der Regler haengt mit `UpdateSourceTrigger=PropertyChanged` an
`TileSize` — jeder Ruck loest einen vollstaendigen Schreibvorgang aus.

### Die Aenderung

Die Kachelgroesse ist genau eine Ansichtsanpassung. Den vorhandenen Weg
verwenden statt eines eigenen: eine `Configure`-Fassade wie bei
`ViewCustomizationStore` und `WindowStateManager`, oder die Live-Instanz
hineinreichen. Das Bedienelement darf keine eigene Einstellungsdatei laden.

**Zusaetzlich:** Einen Architekturtest ergaenzen, der `AppSettings.Load(` in
`Controls/` und `Views/` verbietet. Die bestehende Warnung steht nur als
Kommentar im Code und hat diesen Fall nicht verhindert. Ein Kommentar ist keine
Sperre.

**Nicht anfassen:** `AppSettings.Save()` selbst, die anderen Nutzer der
Live-Instanz.

### Pflichttests

1. Architekturtest: kein `AppSettings.Load(` unter `Controls/` und `Views/`.
   Er muss vor der Aenderung fehlschlagen.
2. Verhaltenstest: Einstellung X aendern, danach die Kachelgroesse aendern,
   danach pruefen, dass X noch den neuen Wert hat.

### Messung, die Erfolg belegt

Beide Tests schlagen vorher fehl und halten nachher. Volle Suite gruen.

---

## AP-3 — Erfundene Meterstaende duerfen nicht als „gemessen" in die Trainingsdaten

**Datei:** `src/AuswertungPro.Next.Infrastructure/Ai/OsdMeterDetectionService.cs`
**Stellen:** Zeile 113 (`SmoothMeterTimeline`), Zeile 212 (`result.Add((t, 0));`).
**Betroffener Weg:** `MeterTimelineService.cs:105` und `:28` →
`TrainingSampleGenerator.cs:195-201`.

### Was heute passiert

Es gibt einen zweiten Meter-Lueckenfueller neben dem neuen. Ihm fehlen alle drei
Klammern, die der neue aus echten Fehlern gelernt hat:

| Klammer | `MeterSequenceGapFiller` | `SmoothMeterTimeline` |
|---|---|---|
| Obergrenze fuer die Lueckenlaenge | ja (10 s) | **nein** |
| Schutz gegen Richtungswechsel (Rueckwaertsfahrt) | ja | **nein** |
| Kennzeichnung geschaetzter Werte | ja (`IsEstimated`) | **nein** |

Der Rueckgabetyp ist `double`, nicht `double?` mit Schaetzflag. Zusaetzlich
extrapoliert der Weg ueber die Raender hinaus, und bei komplett unlesbarem OSD
steht in Zeile 212:

```csharp
result.Add((t, 0));
```

Also eine Zeitreihe aus lauter Null-Metern. `TrainingSampleGenerator` uebernimmt
das ungeprueft und markiert die Quelle als gemessen:

```csharp
detectedMeter = MeterTimelineService.InterpolateMeter(timeline, t);
if (detectedMeter.HasValue)
{
    meterSource = "osd";
```

**Folge fuer die Daten:** Bei einem Video mit unlesbarem OSD bekommt jedes
erzeugte Sample `DetectedMeter = 0`, die Quelle `osd` — also „gemessen" — und
einen fast immer gesetzten `HasOsdMismatch`.

Das ist derselbe Fehler, gegen den der ganze Bogen-Weg gebaut wurde: **Eine
erfundene Null sieht aus wie eine Messung.** Dort ist die Angabe `null` statt
`0,0`, wenn kein Meterstand vorliegt. Hier nicht.

### Die Aenderung

Zwei Schritte, der zweite ist der Pflichtteil:

1. **Sofort:** Den Zweig `result.Add((t, 0));` durch „kein Wert" ersetzen. Eine
   leere Zeitreihe ist ehrlicher als eine Reihe aus Nullen.
2. **Eigentliche Behebung:** `SmoothMeterTimeline`/`InterpolateMissing` durch
   `MeterSequencePlausibility` und `MeterSequenceGapFiller` aus
   `src/AuswertungPro.Next.Application/UseCases/BendSuggestions/` ersetzen und
   `IsEstimated` bis in `TrainingSample.MeterSource` durchreichen: `osd` nur
   noch fuer wirklich gelesene Werte, sonst `osd_geschaetzt` oder gar nichts.

**Achtung bei Schritt 2:** Es gibt bestehende Samples mit `MeterSource = "osd"`,
die in Wahrheit geschaetzt sind. Diese Altdaten nicht umschreiben. Nur der neue
Weg wird sauber; der Altbestand bleibt, wie er ist, und wird im Bericht als
unsicher gekennzeichnet.

### Pflichttests

1. Zeitreihe ohne einen einzigen lesbaren Wert: Ergebnis leer, **nicht** eine
   Reihe von Nullen.
2. Luecke ueber 10 Sekunden: wird nicht gefuellt.
3. Meterstand faellt zwischen zwei Messungen (Kamera faehrt zurueck): keine
   Interpolation darueber hinweg.
4. Ein gefuellter Wert erreicht `TrainingSample` nicht als `MeterSource = "osd"`.

### Messung, die Erfolg belegt

Tests 1 bis 4 schlagen vorher fehl. Zusaetzlich: Ein Trainingslauf auf einem
Video mit unlesbarem OSD erzeugt danach kein einziges Sample mit
`DetectedMeter = 0` und `MeterSource = "osd"`.

---

## AP-4 — Uebersprungene Schutztests duerfen nicht als „bestanden" gelten

**Datei:** `tests/AuswertungPro.Next.Infrastructure.Tests/Backup/JunctionFactAttribute.cs`
**Stelle:** Zeile 14.

### Was heute passiert

```csharp
internal sealed class JunctionFactAttribute : FactAttribute
{
    public JunctionFactAttribute()
    {
        Skip = JunctionTestSupport.UnavailableReason;
    }
}
```

`JunctionTestSupport.Probe()` legt zur Pruefung eine Verzeichnis-Verknuepfung an.
Auf dem Entwicklungsrechner scheitert das mit „Fuer diesen Vorgang sind
Administratorrechte erforderlich". Damit greift der Skip fuer **alle 13**
`[JunctionFact]`-Tests.

Der Auditpruefer hat es ausgefuehrt:
`dotnet test --filter ReparsePointGuardTests|DirectoryMirrorReparsePointTests`
meldet „Fehler: 0, erfolgreich: 3, uebersprungen: 5" — und faerbt gruen.

**Konkret:**
- `DirectoryMirrorReparsePointTests` hat 4 von 4 Tests als `[JunctionFact]` — die
  Klasse laeuft lokal nie.
- In `ReparsePointGuardTests` ist der **einzige** Test, der jemals
  `IsReparsePoint == true` behauptet, ebenfalls `[JunctionFact]`. Die drei
  laufenden Tests pruefen nur Falsch-Faelle.

**Folge:** Waere `ReparsePointGuard.IsReparsePoint` kaputt und wuerde immer
`false` liefern, bliebe der lokale Lauf gruen. Spiegelung und Vollsicherung
koennten dann ueber eine Verknuepfung hinweg fremde Dateien ausserhalb des
Sicherungsziels loeschen oder ueberschreiben — genau der Datenverlust, den
CLAUDE.md mit „Verknuepfungsschutz sichert jede Loeschung ab" als abgedeckt
beschreibt.

### Die Aenderung

Die Faehigkeit **einmal beim Testlauf hart pruefen**, statt sie pro Test still zu
umgehen. Einen zusaetzlichen normalen `[Fact]` ergaenzen, der fehlschlaegt, wenn
`JunctionTestSupport.UnavailableReason` gesetzt ist **und** eine Umgebungsvariable
wie `SEWER_ALLOW_JUNCTION_SKIP` nicht ausdruecklich gesetzt wurde.

Damit gilt: Wer die Tests ohne die noetigen Rechte faehrt, sieht einen roten
Test mit klarer Ansage, statt eines gruenen Laufs ohne Schutzpruefung.

**Parallel, kein Code:** Auf dem Entwicklungsrechner den Windows-Entwicklermodus
einschalten. Dann darf `CreateSymbolicLink` ohne Administratorrechte laufen und
alle 13 Tests fahren wirklich.

**Nicht anfassen:** die 13 Tests selbst, `ReparsePointGuard`, `DirectoryMirror`.
Dieses Paket aendert nur, wie ein fehlendes Recht gemeldet wird.

### Pflichttests

Der neue `[Fact]` ist selbst der Test. Er muss auf einem Rechner ohne
Verknuepfungsrecht rot sein und mit gesetzter Umgebungsvariable oder
eingeschaltetem Entwicklermodus gruen.

### Messung, die Erfolg belegt

Vor der Aenderung: `dotnet test --filter DirectoryMirrorReparsePointTests` meldet
gruen bei 4 uebersprungenen Tests. Nachher: rot mit lesbarem Grund, oder gruen
mit 4 wirklich gelaufenen Tests.

---

## Was ausdruecklich NICHT beauftragt ist

Diese Punkte sind bestaetigt, aber folgenlos genug, um Zeit besser anderswo zu
verwenden. Sie sind hier festgehalten, damit sie nicht verloren gehen und damit
niemand sie fuer uebersehen haelt:

| Punkt | Warum es liegen bleibt |
|---|---|
| Letterbox-Vorverarbeitung dreifach gepflegt | heute byte-gleich, nur Wartungsrisiko |
| `plausibilisiere_sequenz` doppelt (Sidecar und C#) | alle vier Zahlen heute identisch, kein Sidecar-Endpunkt ruft sie |
| Hartkodierter `C:\KI_BRAIN`-Pfad im Bogen-Arbeitspunkt | greift nur auf einem abweichend eingerichteten Rechner |
| Prozessweite Caches im `HoldingFolderDistributor` | wirkt erst bei Ordneraenderungen zur Laufzeit |
| Wissens-ZIP ohne `eval_review` | Vollsicherung und Echtzeitspiegel decken es ab, nur der ZIP-Transferweg ist unvollstaendig |
| Statisches `LastRequestJson` in Prompt-Tests | Testhygiene, kein Produktionsfehler |
| Waechter gegen rohe Fehlertexte umgehbar | bekannt und dokumentiert |

**Ausnahme:** Zwei Stellen in `CLAUDE.md` sind veraltet und sollten bei
naechster Gelegenheit nachgezogen werden — die Klassen-Migration mit falschen
Zahlen und Freigabe-Hashes, und das komplette Bogen-Vorschlags-Subsystem, das
mit 16 Dateien produktiv registriert ist und dort nicht vorkommt. Das ist
Dokumentation, kein Code, und blockiert keines der vier Pakete.

## Grenzen dieses Audits

Es wurde nichts gebaut und die volle Testsuite nicht durchgefahren; nur einzelne
gezielte Testlaeufe zur Pruefung einzelner Behauptungen. Die Aussage „kein
Befund mit Schwere hoch" bezieht sich auf die acht geprueften Dimensionen und
nicht auf das ganze Programm — Bereiche wie Nebenlaeufigkeit unter Last oder das
Verhalten bei vollem Datentraeger wurden nicht untersucht.
