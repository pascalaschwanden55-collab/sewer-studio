# Feld- und Wertelisten-Audit gegen SIA405 · 8. September 2026

**Ergebnis in einem Satz: Die Begriffe sind richtig, die Wege zu ihnen nicht überall.**

Alle zwölf Wertelisten, die in eine XTF führen, stimmen zeichengenau mit der
Modelldatei überein — geprüft gegen die Norm selbst, nicht gegen die eigene
Dokumentation. Gefunden wurden ein fachlicher Übersetzungsfehler, zwei Lücken bei
den Auswahllisten und drei Stellen, an denen das Redesign Pflichtfelder aus den
Standardansichten verdrängt hat. Dazu zwei Punkte zur Klärung, die keine Fehler
sind, aber eine Entscheidung brauchen.

Dieser Bericht ergänzt den [Prüfbericht vom Morgen](PRUEFBERICHT.md) (R1–R8). Der
prüfte Verhalten und Feldzuordnung der Oberfläche; dieser prüft die
Normkonformität der Werte und Auswahllisten.

## Womit geprüft wurde

Massgebend war die Modelldatei selbst, nicht `docs/SIA405-2020-Wertelisten.md`:

- `SIA405_Abwasser_2020_1_2_d_LV95`, Modell `SIA405_ABWASSER_2020_1_LV95`,
  VERSION 29.11.2025 (`.tmp/redesign-sia405-audit/SIA405_2020_1.ili`)
- Basismodell `SIA405_Base_Abwasser_LV95`, VERSION 03.11.2020
  (`.tmp/redesign-sia405-audit/Base_2020.ili`)

Die Wertelisten wurden maschinell aus der Modelldatei gezogen und mit den
Vokabularen im Programm verglichen. Eine eigene Doku kann irren; die Modelldatei
nicht.

Stand: Arbeitsbaum auf `feature/eval-pruefsatz-review`, Commit `0d3ea8d89` plus
82 nicht eingecheckte Änderungen (darunter die gesamte XTF-Änderungslieferung vom
07.09.). Build: 0 Fehler, 0 Warnungen. Tests: 15'913 erfolgreich, 27
übersprungen, **0 Fehler** über alle vier Testprojekte.

## Was nachweislich stimmt

Zwölf Listen, jede zeichengenau und vollzählig gegen das Modell:

| Feld im Modell | Werte im Modell | Im Programm erreichbar |
|---|---|---|
| `Haltung.Material` | 24 | 24 ✓ |
| `Kanal.Nutzungsart_Ist` | 9 | 9 ✓ |
| `Kanal.Verbindungsart` | 13 | 13 ✓ |
| `Kanal.Bettung_Umhuellung` | 14 | 14 ✓ |
| `Kanal.FunktionHydraulisch` | 12 | 12 ✓ |
| `Kanal.FunktionHierarchisch` | 14 Blätter | 14 ✓ |
| `Haltung.Lagebestimmung` | 3 | 3 ✓ |
| `Abwasserbauwerk.Status` | 5 | 5 ✓ |
| `Abwasserbauwerk.Sanierungsbedarf` | 6 | 6 ✓ |
| `Rohrprofil.Profiltyp` | 7 | 7 ✓ |
| `Normschacht.Material` | 4 | 4 ✓ |
| `Versickerungsanlage.Art` | 11 | 11 ✓ |
| `Spezialbauwerk.Funktion` | 35 | 35 ✓ |
| `Organisation.Organisationstyp` | 6 | 6 ✓ |

Die heiklen Schreibweisen sitzen: `Kunststoff_Polyvinilchlorid` mit **i**,
`offenes_Profil` klein, `entlastetes_Mischabwasser` klein, `Beton_unbekannt` statt
`Beton`. Die Trennung zwischen lesbarem Begriff im Programm und Normschreibweise in
der Datei ist in allen Vokabularen durchgezogen — ausser bei den beiden neuen
Bauwerkslisten (siehe B2).

Struktur der Änderungslieferung ebenfalls geprüft: In den betroffenen Klassen ist
nur `Bezeichnung` Pflicht, und die bleibt erhalten. `Letzte_Aenderung` (Pflicht im
Basismodell) setzt der Schreiber selbst. Alle Pflichtbeziehungen `{1}`
(`DatenherrRef`, `DatenlieferantRef`, `EigentuemerRef`, `AbwasserbauwerkRef`,
`vonHaltungspunktRef`, `nachHaltungspunktRef`) bleiben erhalten, die verwiesenen
Objekte werden mitgeliefert. Die weggelassene Geometrie ist im Modell nicht Pflicht.

## Befunde

### B1 · Mittel · `Fettabscheider` wird beim Schreiben zu `andere`

Das Modell führt `Fettabscheider` als einen der 22 gültigen Werte von
`Normschacht.Funktion`. SewerStudio bildet ihn trotzdem auf `andere` ab:

```
SchachtFunktionVokabular.cs:66   new(["fettabscheider"], "Fettabscheider", "andere")
XtfSchachtPlanBuilder.cs:104     "Funktion" => SchachtFunktionVokabular.NachNorm(wert)
```

Wer im Programm „Fettabscheider" wählt, hat in der Datei „andere" stehen. Der
Kommentar im Code nennt den Grund — Abwasser Uri benutzt den Wert in 64'420
Schächten kein einziges Mal — und benennt die Folge selbst: ein aus einer XTF
gelesener Fettabscheider käme als `andere` zurück. Für einen Schacht mit
Fettabscheider ist das ein echter Informationsverlust, und es gibt keinen Grund
dafür: Das Ziel existiert.

**Warum der Wächter das nicht gefunden hat:** `DropdownExportierbarkeitTests`
prüft, **dass** jeder wählbare Wert ein Ziel findet — nicht, **welches**.
`Fettabscheider → andere` besteht diesen Test.

**Korrektur:** `new(["fettabscheider"], "Fettabscheider", "Fettabscheider")`. Beim
Spezialbauwerk stimmt es bereits.

### B2 · Mittel · Die Funktions-Auswahl am Schacht mischt zwei Bauwerksklassen

`SchachtFunktionOptions` hängt zwei Listen aneinander:

```
SchaechtePageViewModel.cs:310-312
  SchachtFunktionVokabular.Auswahl                    (22 Normschacht-Begriffe, lesbar)
  + AbwasserbauwerkVokabular.Spezialfunktionen        (35 Spezialbauwerk-Normwerte, roh)
```

Zwei Folgen:

**Fachlich:** Auf einem Normschacht sind rund zwanzig dieser Einträge ungültig —
`Regenbecken_Fangbecken`, `Duekerkammer`, `Wirbelfallschacht`, `Havariebecken` und
so weiter. Wer sie wählt, merkt nichts; erst der Exportbericht meldet
„passt nicht ins Standardfeld". Umgekehrt kennt die Liste keine Filterung nach der
gewählten Bauwerksart.

**Sichtbar:** Die 35 Spezialwerte erscheinen als rohe Normbezeichner mit
Unterstrichen und zusammengeschriebenen Wörtern (`abflussloseGrube`,
`seitlicherZugang`, `MuldenRigolenversickerung`) direkt neben den 22 sauberen
deutschen Begriffen. Dasselbe gilt für `VersickerungsartOptions`
(`AbwasserbauwerkVokabular.Versickerungsarten`) und `BauwerksartOptions`. Das neue
`AbwasserbauwerkVokabular` ist das einzige Vokabular ohne die Trennung
„lesbarer Begriff im Programm / Normwert in der Datei".

**Wächterlücke:** `DropdownExportierbarkeitTests.Jeder_waehlbare_Schachtwert_findet_ein_Ziel`
prüft `SchachtFunktionVokabular.Auswahl` — also die 22er-Liste, **nicht** die
Liste, die der Benutzer wirklich sieht. Die zusätzlichen 35 Werte sind
ungeprüft.

**Korrektur:** Die Funktionsliste nach gewählter Bauwerksart filtern und für die
Spezial- und Versickerungswerte dieselbe App/Norm-Trennung einführen wie überall
sonst. Den Wächter auf die tatsächlich angebotene Liste umstellen.

### B3 · Mittel · `Nutzungsart` ist in der Tabelle ein Freitextfeld

Es gibt zwei getrennte Wege zu einem Auswahlfeld:

- Formular (Aufklapp-Liste, Eingabefelder) → `FieldCatalog.ComboItems`
- Tabelle (`DataGrid`) → `GridDropdownFieldPolicy`

`Nutzungsart` steht nur im ersten. In `GridDropdownFieldPolicy` fehlt der Eintrag,
also baut `DataPageColumnFactory` eine gewöhnliche Textspalte
(`DataPageColumnFactory.cs:25`, Rückfall auf `DataGridStandardTextColumnFactory`).
Alle anderen SIA405-Felder — `FunktionHierarchisch`, `Verbindungsart`,
`Bettung_Umhuellung`, `Profiltyp`, `FunktionHydraulisch`, `Status`,
`Sanierungsbedarf`, `Lagebestimmung`, `Rohrmaterial` — haben dort einen Eintrag.

Ein in der Tabelle getipptes „Regenwasser" oder „SW" findet beim Export kein Ziel.
Die Nutzungsart ist zugleich der Faktor B2 der Dringlichkeitszahl.

**Korrektur:** `"Nutzungsart"` in `GridDropdownFieldPolicy` aufnehmen
(`AllowFreeText: false`, Quelle `NutzungsartVokabular.Auswahl`) und den
Wächter um einen Abgleich der beiden Listen ergänzen — sie dürfen nicht
auseinanderlaufen.

### B4 · Mittel · Der Eigentümer fehlt in den Standardansichten der Tabelle

Ohne Eigentümer entsteht **kein** Objekt in der Datei: `EigentuemerRef` hat im
Modell die Kardinalität `{1}`, und ohne bekannten Organisationstyp wird das Bauteil
übersprungen. Es ist damit das Feld, das über Erfolg oder Ausfall des ganzen
Exports entscheidet.

- Schächte: `Eigentümer` steht in `SchaechteColumnViewCatalog` nur in
  „Sanierung und Kosten" und in „Alle Spalten" — nicht in „Kompakt".
- Haltungen: `Owner` steht in „Stammdaten" und „Kosten" — nicht in „Kompakt".

„Kompakt" ist seit der Fixwelle 2b die einmalig gesetzte Standardansicht
(`KompaktStartRegel`). In der Ansicht, die der Benutzer nach dem Umbau zuerst
sieht, ist das Pflichtfeld also unsichtbar. Zusätzlich kann es über
„Ansicht anpassen" (`AufklappDetailLayout`, `IsHiddenByUser`) auch im Formular
ausgeblendet werden, ohne Warnung.

**Korrektur:** Eigentümer in „Kompakt" aufnehmen — oder, besser, vor dem Export
sichtbar melden, wie viele Bauteile mangels Eigentümer nicht geliefert werden. Der
Bericht enthält die Information bereits, aber erst nach dem Lauf.

### B5 · Niedrig · Die SIA405-Felder liegen im Sammelthema

Acht Felder der Haltung, die alle direkt in die Datei gehen, sind in
`DataPageRecordDetailsBuilder` keinem der vier Themen zugeordnet und landen damit
in „Weitere Angaben — Felder ohne klare Zuordnung":

`FunktionHierarchisch`, `FunktionHydraulisch`, `Verbindungsart`,
`Bettung_Umhuellung`, `Status`, `Sanierungsbedarf`, `Lagebestimmung`,
`Bruttokosten`

In keiner benannten Spaltenansicht kommen sie ebenfalls vor — nur in
„Alle Spalten". Am Schacht gilt dasselbe für die beiden neuen Felder
`Bauwerksart` und `Versickerungsart`: `SchaechteColumnPolicy.ResolveSchachtDetailGroup`
kennt keinen passenden Wortbestandteil, also fallen sie in „Weitere Angaben".

Das ist dieselbe Wurzel wie R7 im Morgenbericht — die Gruppierung über
Wortbestandteile statt über eine verbindliche Feldliste. Es gehen keine Daten
verloren; die Felder sind nur am schwersten erreichbar, obwohl sie den Kern der
Katasterlieferung bilden.

### B6 · Zur Klärung · Der vollständige Erstexport ist aus der Oberfläche nicht mehr erreichbar

`XtfNeuExportService` beherrscht beides — Vollexport und Änderungslieferung. Es gibt
im gesamten UI aber nur **einen** Aufruf, und der ist festverdrahtet:

```
ExportPageViewModel.Xtf.cs:19   public bool XtfNurAenderungen => true;
ExportPageViewModel.Xtf.cs:68   new XtfNeuExportRequest(..., NurAenderungen: XtfNurAenderungen)
```

Die Beschriftung im XAML ist ehrlich („XTF für den Abgleich erstellen"), der
Bericht auch („Lieferart: nur Handänderungen mit Feldaufträgen"). Nicht
nachgezogen wurden:

- Der Kommentar an derselben Methode: „Neue eigenständige XTF erstellen".
- Der Klassenkommentar: „Neu = Erstexport mit eigenen Kennungen".
- `CLAUDE.md`, das den Weg weiterhin als „Neue eigenständige XTF erstellen ...
  erzeugt eine eigenständige XTF aus dem ganzen Projektstand" beschreibt.
- `XtfExportAuswahl`, das den „Neu"-Weg weiterhin empfiehlt, wenn keine
  Importkopie vorliegt.

Der dokumentierte Anwendungsfall — die 33 Haltungen ohne Katastervorlage —
hat damit keinen Weg mehr. Die Änderungslieferung liefert dafür zu wenig: Sie
reduziert jedes Objekt auf `Bezeichnung` plus handmarkierte Felder und entfernt
die Geometrie (`XtfAenderungsPlanBuilder.cs`, `o with { Felder = felder,
Geometrie = null }`).

**Zu entscheiden:** War das Absicht? Wenn ja, gehören Kommentare, `CLAUDE.md` und
die Empfehlungslogik nachgezogen. Wenn nein, braucht es den zweiten Knopf zurück.

### B7 · Zur Klärung · Jede erzeugte XTF trägt ein zweites, fremdes Modell

`XtfZusatzangaben.Ergaenze` läuft **vor** der Verzweigung und damit in beiden
Betriebsarten:

```
XtfNeuExportService.cs:38   plan = XtfZusatzangaben.Ergaenze(plan, request.Projekt);
XtfNeuExportService.cs:39   if (request.NurAenderungen) ...
```

Sobald ein Bauteil einen der 52 freigegebenen Zusatzwerte trägt — und das trifft
praktisch jedes an, `Bemerkungen`, `Kosten`, `Primaere_Schaeden` oder
`Zustandsnote` genügen — schreibt `XtfZusatzWriter`:

- einen zweiten Eintrag `SewerStudio_Zusatz_2026` in die `MODELS` des Dateikopfs,
- einen dritten Behälter in die `DATASECTION`,
- die Datei `SewerStudio_Zusatz_2026.ili` daneben in den Ausgabeordner.

Fachlich ist das sauber gebaut und wird im Bericht genannt. Der Punkt ist ein
anderer: Ein Empfänger, der die XTF mit dem ilivalidator prüft, braucht dieses
Modell auf seinem Modellpfad. Fehlt es, scheitert die Prüfung **der ganzen
Datei**, nicht nur des Zusatzteils. Wer nur die `.xtf` weiterschickt und die `.ili`
im Ordner liegen lässt, liefert eine Datei, die beim Empfänger nicht validiert.

Der in `CLAUDE.md` festgehaltene erfolgreiche ilivalidator-Lauf („null Fehler",
1.15.0) stammt vom 03.09. und damit von einem Stand **vor** dieser Erweiterung. Er
deckt die heutige Ausgabe nicht mehr ab.

**Empfehlung:** Vor der nächsten Lieferung an Trigonet einmal mit dem
ilivalidator gegenrechnen — einmal mit und einmal ohne die `.ili` daneben — und
das Ergebnis als Beleg ablegen. Falls der reine SIA405-Export je wieder gebraucht
wird (B6), sollte er ohne Zusatzteil auskommen.

### B8 · Niedrig · Doku und Modelldatei widersprechen sich beim Organisationstyp

`docs/SIA405-2020-Wertelisten.md` nennt **7** Werte einschliesslich
`Gemeindeabteilung`. Die im Repository liegende Basismodelldatei (VERSION
03.11.2020) führt **6** — `Gemeindeabteilung` kommt darin nicht vor.

Ohne Folgen für den Code: `EigentumVokabular` benutzt nur die sechs gemeinsamen
Werte. Möglicherweise hat die neuere Basisfassung vom 18.10.2023, die nicht auf der
Platte liegt, den siebten Wert ergänzt. Sollte beim nächsten Modell-Download
geklärt und in der Doku richtiggestellt werden.

### B9 · Niedrig · Zustandsklasse kennt kein ausdrückliches „unbekannt"

`Abwasserbauwerk.BaulicherZustand` erlaubt `Z0` bis `Z4` **und** `unbekannt`. Die
Auswahl im Programm bietet leer und `0` bis `4`. Ein leeres Feld schreibt nichts —
fachlich vertretbar, weil „nicht berechnet" und „unbekannt" nicht dasselbe sind.
Festgehalten, damit die Lücke bewusst bleibt und nicht später als Fehler gilt.

## Was nicht geprüft wurde

- **Kein Lauf gegen den ilivalidator.** Die Strukturprüfung erfolgte gegen die
  Modelldatei von Hand, nicht mit einem Prüfprogramm. B7 bleibt deshalb
  ausdrücklich ein Verdacht mit begründeter Ursache, kein gemessener Fehlschlag.
- **Kein Export mit echten Kundendaten.** Alle Aussagen stammen aus Code und
  Modelldatei.
- **Kein Programmstart.** Die Sichtbarkeit der Felder wurde aus den Katalogen und
  Buildern abgeleitet, nicht am laufenden Programm nachgesehen.
- **Der Revisionsweg** („Bestehende Katasterdaten aktualisieren") wurde nur
  daraufhin geprüft, dass er den Zusatzteil nicht mitschreibt — er läuft über
  `XtfRevisionWriter` und ist von B7 nicht betroffen. Seine Feldabbildung selbst
  war nicht Gegenstand dieses Audits.
- **Die 2015-Fassung** der Wertelisten ist nur aus Kundendateien belegt; VSA
  veröffentlicht das Modell nicht mehr. Ein Vergleich gegen die Norm war dort
  nicht möglich.

## Vorschlag für die Reihenfolge

1. **B1** — eine Zeile, klarer fachlicher Gewinn.
2. **B3** — ein Eintrag in `GridDropdownFieldPolicy`, schliesst eine echte Lücke.
3. **B6** — Entscheidung, dann Kommentare und `CLAUDE.md` nachziehen. Kostet
   nichts ausser Klarheit, verhindert aber eine falsche Erwartung an den Knopf.
4. **B7** — einmal mit dem ilivalidator gegenrechnen, bevor etwas rausgeht.
5. **B2** — Filterung nach Bauwerksart und lesbare Begriffe; grösserer Eingriff.
6. **B4/B5** — gemeinsam mit R7/R8 aus dem Morgenbericht angehen, das ist
   dieselbe Wurzel.
