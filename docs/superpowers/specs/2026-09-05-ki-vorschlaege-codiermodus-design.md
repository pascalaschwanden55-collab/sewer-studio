# KI-Vorschlaege im Codiermodus — Entwurf (2026-09-05)

## Ziel

Die drei gemessenen KI-Helfer (Bogen-Copilot, Rohranfang, Rohrende) sollen dort
helfen, wo codiert wird: im Codiermodus des Players. Bisher gibt es sie nur als
Vorabdurchlauf im Training Studio. Der Mensch bleibt Entscheider; die KI schlaegt
vor, der Mensch bestaetigt, bearbeitet oder lehnt ab.

Entscheide von Pascal (2026-09-05):

1. Der Durchlauf startet **automatisch beim Oeffnen des Codiermodus** im Hintergrund.
2. **Bogen bestaetigen** springt zum Spitzenbild und oeffnet das VSA-Codierfenster
   mit `BCC` vorgewaehlt; die Richtung waehlt der Mensch.
3. **Rohranfang und Rohrende** sind Sprungmarken; Bestaetigen legt BCD/BCE an diesem
   Videomoment an, und am Rohrende wird bei fehlender Haltungslaenge der gelesene
   OSD-Meterstand als Laenge vorgeschlagen.
4. Umsetzung als **eigene Vorschlagsliste** (Weg A), getrennt von der Import-Referenz.

## Nicht Ziel

- Kein Live-Overlay im laufenden Video, keine Erkennung je Bild waehrend der Wiedergabe.
- Keine Modellwahl im Player. Kein 15-Klassen-Detektor, kein Qwen.
- Keine automatische Uebernahme: Ohne Klick entsteht kein Ereignis und kein Trainingsfall.
- Keine Aenderung an den Regeln der beiden Durchlaeufe (Abtastrate, Schwellen, Arbeitspunkt).
  Wer sie aendert, misst die Freigabe neu.

## Ablauf

1. `PlayerWindow` betritt den Codiermodus und startet die Hintergrunddienste
   (`CodingModeBackgroundServicesWorkflow`). Dort kommt ein vierter Schritt dazu:
   `StartSuggestionScan`.
2. Der Schritt prueft den Schalter `AppSettings.CodingSuggestionsEnabled` (Standard `true`)
   und wartet die bestehende KI-Bereitschaftspruefung ab. Ohne erreichbaren Sidecar
   zeigt die Karte den Grund und der Ablauf endet.
3. `CodingSuggestionScanUseCase` (Application, `UseCases/CodingSuggestions/`) ruft
   nacheinander:
   - `IBendSuggestionScanService.ScanAsync` mit dem festen Pin
     (`CodingBendCandidatePin`: ID `bcc_nc15_seed46_20260808`, Gewicht-SHA-256
     `8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114`);
   - `IPipeEndSuggestionScanService.ScanAsync` fuer Rohranfang und Rohrende.
   Nacheinander, weil alle drei Gewichte den Slot `YOLO_TEST` teilen.
4. Das Ergebnis ist ein `CodingSuggestionSet`: Liste der Bogen-Vorschlaege, hoechstens ein
   Rohranfang, hoechstens ein Rohrende, die Meterspur des Videos und je Teil ein
   Status (`Bereit`, `NichtVerfuegbar(Grund)`, `Fehler(Grund)`).
5. Sobald das Set mindestens einen Vorschlag enthaelt, ruft der Ablauf
   `ICodingSuggestionExposure.MarkExposed(haltung)`. Damit meldet
   `CodingEventToSampleMapper` fuer diese Haltung `SuggestionShown`.
6. Verlassen des Codiermodus oder Schliessen des Fensters bricht den Durchlauf ueber
   den `CancellationToken` ab. Ein Abbruch ist kein Fehler und zeigt keinen Grund.

Jeder Teil faellt einzeln aus: Fehlt der Arbeitspunkt des Bogen-Kandidaten oder passt
der Hash nicht, gibt es keine Bogen-Vorschlaege, Anfang und Ende laufen trotzdem. Ein
technischer Fehler mitten im Durchlauf gilt nie als "kein Vorschlag"; der Teil bekommt
den Status `Fehler` mit Text.

## Meterspur

Der Bogen-Durchlauf liest ohnehin fuer jedes Bild den OSD-Meterstand, prueft die Folge
(`MeterSequencePlausibility`) und fuellt Luecken (`MeterSequenceGapFiller`). Diese Spur
wird additiv als `BendSuggestionScanResult.MeterTrack` (Zeit, Meter, geschaetzt)
herausgegeben. Die Training-Studio-Anzeige aendert sich dadurch nicht.

`CodingSuggestionMeterLookup` (reine Rechnung) liefert zu einer Videosekunde den
naechsten Spurwert innerhalb von 1,5 s. Ein geschaetzter Wert wird als geschaetzt
weitergegeben; ohne Wert innerhalb der Toleranz gibt es keinen Meter. Faellt der
Bogen-Teil aus, gibt es keine Spur und damit am Rohrende keinen Laengenvorschlag.

## Anzeige

- Neue Karte **"KI-VORSCHLAEGE (n)"** im Seitenpanel des Codiermodus neben "Import".
  Kopfzeile waehrend des Durchlaufs: "KI prueft Video … 43 %". Danach die Zahl der
  offenen Vorschlaege. Bei Ausfall eines Teils eine Zeile mit dem Grund.
- Zeilen: Art, Ort, Staerke.
  - Bogen: "Bogen · Meter 9,42 · stark" (geschaetzt: "Meter ca. 9,4"; nicht lesbar:
    "Sekunde 87 (Meterstand nicht lesbar)"; niemals `0,0`).
  - Rohranfang/Rohrende: "Rohranfang · Sekunde 4 · Abnahme 85 %" beziehungsweise
    "Rohrende · Sekunde 143 · Abnahme 89 %". Die Prozentwerte sind die gepinnten
    Abnahmewerte, keine Bildkonfidenz.
- Kontextmenue und Doppelklick: **Springen**, **Bestaetigen**, **Ablehnen**.
  Bestaetigte Zeilen bleiben ausgegraut mit Haken stehen; abgelehnte verschwinden.
- **Zeitleistenmarker**: eigener `SuggestionMarkerCanvas` unter dem bestehenden
  `DamageMarkerCanvas`, gleiche Spurbreite, Position nach Videozeit
  (`Sekunde / Dauer`). Zweite Farbe ueber ein bestehendes Theme-Token; kein fester
  Farbwert. Klick springt. Die Lage rechnet `SuggestionMarkerLayout` (rein).
- Icons als `ui:FluentIcon`, sichtbare Texte mit echten Umlauten, Schriftgroessen ueber
  die `Text…`-Tokens (Waechter `DesignAudit…`).

## Bestaetigen

- **Bogen**: Sprung auf `PeakTimeSeconds`, dann der bestehende Weg
  `CodingCodeExplorerSeedSelectionWorkflow` mit Vorwahl `BCC`. Der Mensch waehlt die
  Richtung (`BCCAA` … `BCCYB`). `MeterStart` ist der Vorschlagsmeter; ist er
  geschaetzt oder fehlt er, greift die normale Meterermittlung des Codiermodus.
  Abbruch im Codierfenster laesst den Vorschlag offen.
- **Rohranfang**: Sprung, dann Anlegen eines BCD-Ereignisses bei 0,00 m mit der
  Videozeit des Vorschlags. Das automatische Grenzereignis beim Uebernehmen
  (`ProtocolBoundaryService`) erkennt das vorhandene BCD und legt kein zweites an.
- **Rohrende**: Sprung, Meter aus der Meterspur. Ist `Haltungslaenge_m` leer und der
  Meter nicht geschaetzt, fragt ein Dialog: "Laenge 42,35 m aus dem Video
  uebernehmen?" Ja schreibt `Haltungslaenge_m` mit `FieldSource.Protocol` wie ein
  BCE-Wert und ist damit unterhalb echter Importquellen. Danach entsteht das
  BCE-Ereignis an dieser Videozeit mit diesem Meter. Ohne Meter entsteht das
  BCE-Ereignis ohne Laengenvorschlag; die bestehende Kette
  (`Haltungslaenge_m -> Laenge_m -> BCE -> Handeingabe`) bleibt unveraendert.
- Bestaetigte Grenzereignisse werden wie heute nicht als Trainingsfall gespeichert.
  Ein bestaetigter Bogen geht den normalen Weg der Handcodierung und traegt
  ueber das Sitzungsgedaechtnis `SuggestionShown`.

## Bausteine

| Schicht | Datei | Zweck |
|---|---|---|
| Application | `UseCases/CodingSuggestions/CodingSuggestionModels.cs` | `CodingSuggestion`, `CodingSuggestionSet`, Status, `CodingBendCandidatePin` |
| Application | `UseCases/CodingSuggestions/CodingSuggestionScanUseCase.cs` | Reihenfolge, Teilausfall, Abbruch, Gedaechtnis |
| Application | `UseCases/CodingSuggestions/CodingSuggestionMeterLookup.cs` | Meterspur-Nachschlag (rein) |
| Application | `UseCases/BendSuggestions/BendSuggestionScanUseCase.cs` | additiv `MeterTrack` im Ergebnis |
| Application | `UseCases/CodingModeBackgroundServicesWorkflow.cs` | vierter Schritt `StartSuggestionScan` |
| UI/Player | `CodingSuggestionsOwner.cs` | ObservableCollection, Zustand je Zeile |
| UI/Player | `SuggestionMarkerLayout.cs` + `SuggestionMarkerController.cs` | Markerlage (rein) und Zeichnung |
| UI/Views | `PlayerWindow.Coding.Suggestions.cs` | Start, Abbruch, Springen, Bestaetigen, Ablehnen |
| UI/Views | `PlayerCodingSidePanel.xaml` | Karte "KI-Vorschlaege" |
| UI | `AppSettings.cs`, `SettingsPage.xaml` | Schalter `CodingSuggestionsEnabled` |
| UI | `ServiceProvider.CodingSuggestions.cs`, `ServiceProviderRegistrationMap.cs` | Verdrahtung, Zaehler 157 -> 158 |

Keine neue Datei unter `UI/Ai` (eingefroren). Kein neuer Sidecar-Endpunkt, kein
neues NuGet-Paket. VRAM: Bogen-Kandidat und Lernstufen teilen `YOLO_TEST`; die
Live-KI des Codiermodus nutzt andere Slots. Das Budget von 29 GB bleibt.

## Tests

- `CodingSuggestionScanUseCaseTests`: Reihenfolge Bogen vor Anfang/Ende; Abbruch
  wirft `OperationCanceledException` und markiert nichts; Bogen-Teil faellt aus,
  Anfang/Ende bleiben; technischer Fehler wird `Fehler`, nie leere Liste;
  `MarkExposed` nur bei mindestens einem Vorschlag; Schalter aus startet nichts.
- `CodingBendCandidatePinTests`: ID und Hash sind die gemessenen Werte und stimmen
  mit der Training-Studio-Konstante ueberein.
- `CodingSuggestionMeterLookupTests`: naechster Wert, Toleranz 1,5 s, geschaetzt
  bleibt geschaetzt, ausserhalb Toleranz kein Wert.
- `BendSuggestionScanUseCaseTests`: `MeterTrack` traegt jede gefuellte Sekunde;
  bestehende Erwartungen unveraendert.
- `SuggestionMarkerLayoutTests`: Lage bei 0 s, Ende, ausserhalb der Dauer.
- `CodingSuggestionsOwnerTests`: Bestaetigen graut aus, Ablehnen entfernt, Zaehler.
- Grenzereignis: bestaetigter Rohranfang erzeugt beim Uebernehmen kein zweites BCD;
  bestaetigtes Rohrende mit Laenge setzt `Haltungslaenge_m` mit `FieldSource.Protocol`
  und ersetzt keinen vorhandenen Wert.
- `ServiceProviderRegistrationTests`: 158.
- Waechter bleiben gruen: `UiAiFreezeArchitectureTests`, `DesignAudit…`,
  `DropdownExportierbarkeitTests` (unberuehrt).

## Offen bleibt

- Ob der Durchlauf bei sehr langen Videos (ueber 10 Minuten) gedrosselt werden soll,
  zeigt der echte Gebrauch. Erste Fassung: keine Drossel, Fortschritt sichtbar.
