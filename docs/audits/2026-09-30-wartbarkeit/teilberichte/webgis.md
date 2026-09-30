# Wartbarkeitsaudit WebGIS-Teilsystem (30.09.2026)

## 1) Kurzfazit
Das Teilsystem ist besser geschnitten, als die Bauzeit (21.-28.09.) erwarten liesse: Application ist WPF-frei (5'676 Zeilen, 36 Dateien), keine Datei ueber 760 Zeilen, Faltung/Klartext-Abgleich laeuft wirklich ueber EINE Funktion (`WebGisBegriffe.Falte` via `WebGisHandwertKarte.Falte`). Die UI enthaelt keine Fachregeln, aber einen 121-Zeilen-Ablauf. Die Schwachstellen sind klein und konkret: (a) refId-Literale ausserhalb der Karten (inkl. Breite/Hoehe doppelt, ein Konflikt mit "OFFEN" in der Doku), (b) zwei gleichnamige, gegenlaeufige `WebGisFuehrt`-Regeln (Senden vs. Holen) und ein dritter Namensvetter, (c) eine zweite, andere Text-Normalisierung im Sanierungskatalog, (d) Positionen als Beutel mit vielen veraenderlichen Feldern, (e) kopierte Fakes/Handler in den Tests.

## 2) Befundtabelle
| ID | Prio | Titel | Nutzen |
|---|---|---|---|
| WG-A | P2 | refIds ausserhalb der drei Karten (Breite/Hoehe doppelt, Baujahr-Schutz-Literal, 27 in Aktenfelder) | Ein Ort fuer jede refId; "live geprueft"-Status auffindbar |
| WG-B | P2 | `WebGisFuehrt` dreifach mit gegenlaeufiger Bedeutung (Senden: nie schreiben, Holen: WebGIS ueberschreibt) | Verwechslung bei Aenderung an Eigentuemer/Betreiber ausschliessen |
| WG-C | P3 | Zweite Normalisierung `WebGisSanierungKatalog.Normalisiert` neben `Falte` | Eine Vergleichsregel je Text |
| WG-D | P2 | `WebGisExportPosition`/`WebGisImportPosition`: Zustand aus Flags und Listen, nicht aus dem Typ | Zustand einer Position lesbar; weniger Kombinationsfehler |
| WG-E | P3 | `ExportWebGisBereich.SchreibeAsync` (121 Zeilen) traegt Ablaufentscheidungen im ViewModel | Ablauf testbar ohne WPF |
| WG-F | P3 | "Live nicht belegt" nur als Freitextkommentar, nicht abfragbar | Offene Annahmen sichtbar zaehlbar |
| WG-G | P3 | Test-Fakes kopiert (2x `FakeClient`, 2x `FakeHandler`, ~8 `Stand`-Bauer) | Weniger Kopie bei Schnittstellenaenderung |

## 3) Befunde im Detail

### WG-A (P2) refIds ausserhalb der Karten
Beleg (Literale `"xxxxxxxx-xxxx-..."` in src, WebGis): `WebGisFeldkarte.cs` 22, `WebGisHandwertKarte.cs` 27, `WebGisSanierungFeldkarte.cs` 11, aber auch `WebGisImportAktenfelder.cs` 27 (Schachtmaske -> Akte, `WebGisImportAktenfelder.cs:36-60`), `WebGisImportPlanBuilder.cs:183-184` (2), `WebGisGeschuetzteFelder.cs:29` (1).
- Konkretes Doppel: `BreiteRef = "902695a4-..."` / `HoeheRef = "d06f8d1f-..."` in `WebGisImportPlanBuilder.cs:183-184` sind wortgleich dieselben refIds wie `WebGisHandwertKarte.cs:45-47` (Lichte_Breite/DN/Lichte_Hoehe). Zwei Quellen fuer dieselbe Kennung.
- `WebGisGeschuetzteFelder.cs:29`: `"e2fddd0d-b1f0-bc99-bc54-95bc6d2d5b1a"` ("Baujahr/Ersatzjahr") steht nirgends in `WebGisFeldkarte` (dort nur `HaltungBaujahrRef` 72b0bc78..., `SchachtBaujahrRef` e35e99dc...).
- Doku-Widerspruch: CLAUDE.md nennt d06f8d1f unter den drei "erschlossenen und FALSCHEN" refIds und spaeter "Breite/Hoehe OFFEN, d06f8d1f einmal Breite, einmal Hoehe". Im Code steht d06f8d1f aktiv als "Höhe [mm]" (`WebGisHandwertKarte.cs:47`) und wird beim Holen gelesen (`WebGisImportPlanBuilder.cs:411-412`) - ohne Markierung im Code, dass dieser Wert unklar ist.
Warum bremst es: Eine Korrektur der Breite/Hoehe-Zuordnung muss an mindestens drei Stellen erfolgen; ein vergessenes Literal bleibt still falsch (Sende- und Holen-Weg wuerden verschieden lesen).
Empfehlung: `BreiteRef`/`HoeheRef` und den Baujahr-Ersatzjahr-Schluessel als benannte Konstanten in `WebGisFeldkarte` (mit XML-Kommentar "geprueft/offen") anlegen; `WebGisImportPlanBuilder` und `WebGisGeschuetzteFelder` verweisen darauf; HandwertKarte nutzt dieselben Konstanten. Aktenfelder-Tabelle darf bleiben (eigene Karte), sollte aber in Namen/Kommentar als "Schacht-Aktenkarte" gefuehrt werden.
Abnahme: Suche nach `"902695a4` und `"d06f8d1f` und `"e2fddd0d` in src ergibt je genau EINEN Treffer; ein Test haelt fest, dass Import- und Handwertkarte dieselbe Breite-refId nennen.

### WG-B (P2) `WebGisFuehrt`: drei Namen, gegenlaeufige Bedeutung
Belege:
- `WebGisHandwertKarte.cs:91-95`: `WebGisFuehrt(feld)` fuer {Eigentuemer, Eigentümer, Betreiber} - Bedeutung Senden: "wird nie aus SewerStudio geschrieben".
- `WebGisImportUseCase.cs:486`: `private static bool WebGisFuehrt(string feld) => feld == FieldKeys.Owner;` - Bedeutung Holen: "WebGIS ueberschreibt sogar die Handeingabe" (`:470`, `:480`). Nur Eigentuemer, nicht Betreiber (Betreiber laeuft ueber `WebGisImportPlanBuilder.BetreiberFeld`, `WebGisImportUseCase.cs:347`, `:437`).
- `WebGisImportPlanBuilder.cs:245`: `FuehrtWebGis(...)` (dritter Name, Plan-Erzeugung).
- Dazu je eigene Pruefstelle im Export: `WebGisExportPlanBuilder.cs:191,259` und `WebGisExportUseCase.cs:269`.
Warum bremst es: Der Entscheid "das WebGIS fuehrt Eigentuemer/Betreiber" steht in zwei Richtungen an vier Orten mit verschiedener Feldmenge (Owner-String-Vergleich ohne Faltung vs. gefaltetes Set inkl. Betreiber). Eine Aenderung der Feldmenge (z. B. neues Fuehrungsfeld) trifft nur eine Richtung.
Empfehlung: Eine kleine Klasse `WebGisFuehrungsfelder` (oder Konstanten in `WebGisHandwertKarte`) mit zwei klar getrennten Namen `NieSenden(feld)` und `HolenUeberschreibtHand(feld)`; die drei privaten/gleichnamigen Methoden entfallen bzw. delegieren.
Abnahme: Kein zweites `WebGisFuehrt` im Repo; Test pro Richtung mit Eigentuemer UND Betreiber gruen.

### WG-C (P3) Zweite Normalisierung
Beleg: `WebGisSanierungKatalog.cs:62-68` `Normalisiert` (Trim, Regex um `,` und `/`, doppelte Leerzeichen) - ohne Gross/Klein, ohne Umlaut. Daneben `WebGisMassnahmenVergleich.cs:41` vergleicht Art/Status/Verfahren mit `Falte`. Der Katalog (Klartext -> Schluessel) und der Vergleich (Klartext <-> Klartext) folgen also verschiedenen Regeln: "Punktuell, Abzweig/Stutzen" wird im Katalog toleriert, in `Gleich` nicht (Falte kennt keine Trennerregel; kann ich nicht ohne Blick in `WebGisBegriffe.Falte` beweisen - Vermutung, nicht geprueft).
Empfehlung: Pruefen, ob `Falte` die Trennerregel aufnehmen kann, oder `Normalisiert` klar als "Katalogsuche" kommentieren und beide in einem Test gegeneinander stellen.
Abnahme: Ein Test mit "Punktuell, Abzweig/Stutzen" vs. "Punktuell,Abzweig / Stutzen" ergibt in Katalog und Vergleich dasselbe Ergebnis (oder dokumentierte Abweichung).

### WG-D (P2) Positionen als veraenderliche Beutel
Belege `WebGisModelle.cs:78-125`: `WebGisExportPosition` hat 8 `init`-Felder, 5 `List`-Felder (`Aenderungen`, `Vergleich`, `Vorschlaege`, `Sperren`, `Hinweise`) und 5 mutable Zustandsflaggen (`Geschrieben`, `VomServerBestaetigt`, `Nachgeprueft`, `SchreibFehler`, plus berechnetes `Schreibbar`). `WebGisLesestand` (`:17-58`) hat 6 Dictionaries/Listen plus `Subtyp {get;set;}`. `WebGisSanierungPosition` (25 public Mitglieder, `WebGisSanierungModelle.cs`) traegt zusaetzlich `Ungeklaert`.
Zustand ist nur aus der Kombination ablesbar (z. B. "geschrieben aber nicht bestaetigt", WG05: `Geschrieben && !Nachgeprueft`); jeder Leser (`WebGisUebersicht.Aus` 103 Zeilen/43 Entscheidungen `WebGisUebersicht.cs:83`, `WebGisExportBericht`, `WebGisPlanVergleich`, ViewModel `:369-371`) leitet ihn neu ab (`ExportWebGisBereich.cs:369-371`: `Exists(p => p.SchreibFehler is not null) || ... s.Ungeklaert is not null`).
Empfehlung (klein): pro Position eine berechnete Eigenschaft `Ausgang` (enum: Offen, Gesperrt, Geschrieben, Bestaetigt, Nachgeprueft, Fehler, Ungeklaert) im Modell, und Berichte/UI nutzen nur sie. Keine Umbauten der Felder.
Abnahme: `WebGisUebersicht`, `WebGisExportBericht` und `ExportWebGisBereich` enthalten keine eigene Kombination aus `Geschrieben/SchreibFehler/Ungeklaert/Nachgeprueft`.

### WG-E (P3) Ablauf im ViewModel
`ExportWebGisBereich.SchreibeAsync`, `ExportWebGisBereich.cs:284-404`, 121 Zeilen, Entscheidungsproxy 29 (groesste UI-Methode des Teilsystems). Dort: frischer Plan, Plan-Vergleich (`WebGisPlanVergleich.Gleich`), Abbruch-Texte, Log/Bericht-Schreiben, Ausgang->Meldungstext-Zuordnung (`:359-371`), Toast-Entscheid. Die Fachregeln stehen korrekt in Application (`WebGisSendenAblauf`), aber die Uebersetzung "Ausgang -> ACHTUNG-Text" und "fehlgeschlagen?" ist UI-seitig und nicht ohne WPF-Dienste testbar.
Empfehlung: Die Text-/Fehlschlag-Zuordnung (`switch` `:359-371`, `fehlgeschlagen` `:368-371`) nach `WebGisSendenAblauf`/`WebGisExportBericht` (Application, rein) verschieben; ViewModel ruft nur.
Abnahme: `SchreibeAsync` unter ca. 80 Zeilen; Test der Ausgang-Texte ohne `ExportWebGisBereich`. (`WebGisHolenAblauf.OeffneAsync` 71 Zeilen ist unauffaellig.)

### WG-F (P3) "Live nicht belegt" nicht abfragbar
Belege: nur 3 Freitext-Vorkommen in Application/WebGis (`WebGisFeldkarte.cs:40,49,66`) plus Dokuabschnitt. CLAUDE.md fuehrt weitere unbelegte Annahmen (Bezeichnung-refIds, Eigentuemer-/Betreiber-Liste "Bund", `LeseMassnahmeAsync`, "Beginn"/Zeitpunkt). Der Code zeigt sie nicht einheitlich; Bsp. `WebGisMassnahmenVergleich.cs` beschreibt das Jahr "aus der Massnahme", die Modelleigenschaft heisst aber weiter `Beginn` (`WebGisSanierungModelle.cs:10-14`, Kommentar sagt "Spalte heisst Zeitpunkt") - Name und Inhalt driften.
Empfehlung: In `WebGisFeldkarte` ein einheitliches Etikett im XML-Kommentar (`Status: live geprueft dd.mm.` / `Status: Inventur, offen`) fuer jede refId-Konstante; `Beginn` umbenennen oder Alias `Zeitpunkt` einfuehren.
Abnahme: `grep "Status: offen"` listet alle unbelegten refIds; kein Kommentar widerspricht dem Eigenschaftsnamen.

### WG-G (P3) Kopie in den Tests
Belege (Tests/Infrastructure.Tests/WebGis, 7'223 Zeilen, 382 Fact/Theory):
- Zwei `FakeClient : IGeonisWebGisClient` mit unterschiedlicher Ausstattung: `WebGisExportUseCaseTests.cs:250` und `WebGisImportUseCaseTests.cs:18`; beide zaehlen `NamensSuchen`/`IdLesungen` (Kopie), der eine schreibt-wirkt-Logik, der andere Massnahmen. Neues Interface-Mitglied = zwei Anpassungen.
- Zwei `FakeHandler : HttpMessageHandler` (`GeonisWebGisClientTests.cs:27`, `GeonisWebGisClientRobustheitTests.cs:33`, plus `StatusHandler` `:494`), fast gleich (Body lesen, "saveData" mitschreiben).
- `LayoutJson()` und `ZustandRef/BemRef`-Konstanten in beiden Client-Testdateien; `Stand`-Bauer verstreut: `HaltungStand` (2x in ExportUseCaseTests, `:126`, `:412`), `StandMitDatum`, `StandMitLaenge`, `SchachtStand*`, `Stand`/`StandMitOrganisation`/`StandMitBedarf` (ImportPlanBuilderTests), `Stand` (TypAaTests).
Verteilung: Regeln/Planer (Export/Import PlanBuilder, Handwert, Begriffe, Bericht, Uebersicht) etwa die Haelfte; Use-Case mit Fake (Export 771 + 4 Teildateien, Import 447 + 3); Client (2 Dateien, ~1'100 Zeilen); UI nur 2 Dateien (191 + 126). Ausgewogen; die Kopie ist das einzige Muster. `WebGisExportUseCaseTests` ist eine 5-teilige partial-Klasse (~1'900 Zeilen).
Empfehlung: Gemeinsame `WebGisTestBausteine` (interner Ordner `WebGis/Support`) mit einem konfigurierbaren `FakeGeonisClient` und einem `FakeHttpHandler`; Stand-Bauer als Builder.
Abnahme: je nur ein `FakeClient : IGeonisWebGisClient` und ein `FakeHandler` im Testordner.

## 4) Was gut ist (erhalten)
- Schichtung: Planer (`WebGisExportPlanBuilder` 446, `WebGisImportPlanBuilder` 446, rein) / UseCases (760, 493) / Client (451 + 292, getrennt nach Sanierung) / UI-Fassade (`ExportWebGisBereich` 438, `WebGisHolenAblauf` 111). Kein Fachcode im UI ausser WG-E.
- Groesste Methode `WebGisExportPlanBuilder.Baue` 131 Zeilen, Entscheidungsproxy 19 - unkritisch; die echten Entscheidungs-Spitzen sind `WebGisImportUseCase.Uebernimm` (120 Z., 54) und `WebGisUebersicht.Aus` (103 Z., 43); beide durch Tests (`WebGisImportUebernahmeTests` 282, `WebGisUebersichtTests` 159) gedeckt.
- "EINE Regel"-Stellen halten mehrheitlich: Faltung (`WebGisHandwertKarte.Falte` -> `WebGisBegriffe.Falte`, ~30 Aufrufer, kein zweites `ToLower`-Gebilde ausser Wert-Tabelle `WebGisImportWert.cs:72`), Massnahmen-Vergleich (`WebGisMassnahmenVergleich`), Wert-Ersetzbarkeit (`IstErsetzbar` `WebGisImportUseCase.cs:461`), Schutzfelder (`WebGisGeschuetzteFelder` nutzt `WebGisFeldkarte`-Konstanten).
- Keine `TODO/FIXME`, keine Datei im Teilsystem ueber 800 Zeilen.
- Regelwaechter gibt es fuer die heiklen Stellen (Schreibschutz, Geschuetzte Felder, Kanalfirma, Identitaet).

## 5) Grenzen der Pruefung
- Nur lesend; kein Build, kein Testlauf. Aussagen zu Verhalten sind aus Code/Kommentaren, nicht aus Ausfuehrung.
- Tests nicht Zeile fuer Zeile gelesen; Kopie in WG-G ist an den Klassendeklarationen und Signaturen belegt, nicht per Diff.
- WG-C ist Vermutung (Inhalt von `WebGisBegriffe.Falte` nicht gelesen).
- Ob d06f8d1f live Hoehe oder Breite ist, laesst sich nur am Objekt im WebGIS klaeren; ich melde nur, dass der Code es nicht als unklar markiert.
- `tools/WebGisLesepruefung` (Main 92 Zeilen) und `obj/`-Dateien nicht bewertet. Robustheitsfehler (Audits 22.-28.09.) nicht erneut gemeldet.
