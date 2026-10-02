# Deepscan Codequalität und Wartbarkeit (02.10.2026)

Prüfstand: `master` fbff3dd4d. Vier Prüfer haben nur gelesen; die Teilberichte stehen unter
[teilberichte/](teilberichte/). Ausgelassen wurden die Bereiche, in denen Codex gerade umbaut
(Player, Projektimport-Orchestrator, Haltungsverteilung, Sicherungsziel, Haltungsgrafik,
`ServiceProvider`, PDF-Import). Grundlage war der Wartbarkeitsaudit vom 30.09.; bekannte Befunde
sind nur aufgeführt, wenn sie noch offen sind oder sich verschlechtert haben.

## Kurzfazit

Die Grössen sind stabil: Seit Welle 3 ist nichts gewachsen, die Sperrklinken halten (28 Methoden
über 200 Zeilen, keine Datei über 1000 Zeilen, 237 leere `catch`, 11 Klongruppen). Der grösste
Hebel liegt nicht mehr in der Grösse, sondern an drei anderen Stellen:

1. **Stille Lücken**, die einen falschen «alles gut»-Eindruck geben: unlesbare Ordner fallen
   aus Import und Verteilung, ohne dass es gemeldet wird (R1). Der Eval-Schutz hat je nach Weg
   drei verschiedene Bedeutungen von «Ordner fehlt» (A1/R2). 16 Python-Tests laufen in keiner
   GitHub-Prüfung (T1).
2. **Doppelpflege**: Die Haltungs- und die Schachtseite bestehen aus 12 Datei-Paaren, die zu
   59–91 % gleich sind. Seit 01.09. musste mehr als jeder dritte Commit beide ändern, und ein
   Unterschied ist belegt (A2). Dazu kommen einige doppelt geführte Fachregeln (A3–A5) und
   9 Klongruppen mit echter Doppelpflege (B4–B6).
3. **Testnetz an Schreibstellen**: Drei Skripte, die Gold- oder Registerdaten verändern, haben
   keinen Test (T2). Die Handwert- und die Pfadschutz-Regel sind nicht an jeder Schreibstelle
   festgehalten (T5/T6).

Gut und zu erhalten: Sperrklinken und Architekturwächter, die Schritt-Aufteilung von KI-Pipeline,
XTF-Rückweg und Gold-Speichern, HttpClient-Lebensdauer, globale Fehlerbehandler,
BestEffort-Muster, PDF-Zusammenführungsprüfung. Alle 237 leeren `catch` tragen einen Kommentar;
200 davon sind gewollt.

## Selbst nachgeprüft (Hauptagent)

- R1: Die Dateisuche (`SafeFileEnumeration`) meldet übersprungene Ordner nur, wenn der Aufrufer
  eine Liste mitgibt. Von 53 direkten Aufrufen tun das 2.
- A1/R2: `TrainingSampleFileStore.FilterEvalContamination` gibt bei fehlendem oder leerem
  Eval-Ordner alle Samples ungefiltert zurück (Z. 282–285). Der YOLO-Export prüft über einen
  eigenen strengen Leser; ein Durchschlag bis in den Export ist nicht belegt.
- T1: 16 `test_*.py` unter `tools/`; `ci.yml` ruft pytest nur für `sidecar/` und
  `training/scripts/tests` auf, unittest nur für `integrations/qgis`.
- A2: «Leere Felder aus QGIS» meldet auf der Haltungsseite das Ergebnis und frischt die Tabelle
  auf (`MeldeFelderExternErgaenzt`), auf der Schachtseite fehlt beides
  (`SchaechtePageViewModel.QgisNachfuellen.cs`). Gespeichert wird trotzdem; die Anzeige ist
  kurz veraltet.

## Befunde (Übersicht)

| ID | Prio | Thema | Grösse |
| --- | --- | --- | --- |
| R1 | P1 | Unlesbare Ordner fallen still aus Import und Verteilung | M |
| A1/R2 | P1 | Eval-Schutz: vier Lesewege, drei Bedeutungen von «fehlt» | M |
| T1 | P1 | 16 Python-Tests unter `tools/` ohne CI (inkl. Sicherheitstest der Prüfserver) | S |
| T2 | P1 | 3 Skripte, die Gold/Register verändern, ohne Test | M |
| A2 | P1 | Haltungs- und Schachtseite als Parallelbestand (12 Paare) | M |
| T3 | P2 | CI ohne `timeout-minutes` und `--blame-hang` | S |
| R3 | P2 | Befundfotos: drei Temp-Rückfälle ohne Hinweis | S |
| R4 | P2 | Abbruch erreicht die Verteilung im Import nicht | M |
| R5 | P2 | Schattenauswertung speichert Rechenfehler wie ein Ergebnis | S |
| R6 | P2 | Training Center: Verteilung im UI-Thread, ohne Abbruch und Pfadwächter | M |
| R7 | P2 | Wächter gegen leere `catch` verlangt nur «irgendeinen» Kommentar | S |
| A3 | P2 | Meterstand am Bild: zwei widersprüchliche Regeln (1,5 s gegen unbegrenzt) | S |
| A4 | P2 | `Datum_Jahr` und Haltungslänge je Leser anders gedeutet | M |
| A5 | P2 | Junction-Schutz in rund 20 lokalen Kopien | M |
| A6 | P2 | KI-Dienste ein zweites Mal ausserhalb des Containers erzeugt | S–M |
| A7 | P2 | 15 Seiten-ViewModels bekommen den ganzen Container; Projekt-Laden im `ShellViewModel` | M |
| A8 | P2 | `UI/Ai`: drei Kandidaten für `Application/UseCases` | je S–M |
| B2/B7 | P2 | Brennpunkte `ShellViewModel`, `AppSettings`, Schachtseite, WebGIS-Use-Cases | M |
| B4–B6 | P2/P3 | Klongruppen mit echter Doppelpflege (Nova-Controller, Dossier-PDF, Stores) | S je |
| T4–T6 | P2 | Testhilfen (87 TempDirectory-Klassen), Netz für Pfadschutz und Handwerte | M |
| R8–R10, A9, A10, T7–T9 | P3 | Kleinere Punkte, siehe Teilberichte | S |

## Entscheide für Pascal (vor der Umsetzung)

- **E1 (A1/R2):** Soll das Speichern von Gold-Samples **sperren**, wenn der Eval-Ordner fehlt
  oder unlesbar ist (statt wie heute ungefiltert durchzulassen)? Empfehlung: Ja, mit klarer
  Meldung. SewerStudio läuft nur auf deinem Rechner, und dort ist der Ordner immer vorhanden.
- **E2 (A3):** Soll beim Handeintrag ein gemerkter OSD-Meter nur gelten, wenn er höchstens 1,5 s
  alt ist, wie in der Anzeige? Empfehlung: Ja, eine Regel.
- **E3 (R10):** «Handwert, auch bewusst leer» gegen `FuelleLeeresFeld`: Darf ein Import ein Feld
  füllen, das du bewusst leer gelassen hast? Empfehlung: Nein, wie in CLAUDE.md beschrieben.

## Umsetzungsplan (kleine, einzeln prüfbare Pakete, je eigener PR nach `master`)

**Welle 1: Schutz und Sichtbarkeit (P1, ohne Codex-Bereiche)**
1. T1 + T3: Python-Tests unter `tools/` in die CI aufnehmen; Zeitgrenzen und `--blame-hang`.
2. R1: übersprungene Ordner in KINS-, WinCan-, Dichtheits- und Medien-Wegen im Importbericht
   melden. Die Haltungsverteilung erst nach Codex' Umbau.
3. A1/R2 (nach E1): ein gemeinsamer Eval-Schutz-Leser mit einer Bedeutung von «fehlt».
4. T2: Tests für `import_gold_labels.py`, `remove_eval_contaminated_from_register.py`,
   `repair_inbox_gold_holding_ids.py`.
5. R3: Temp-Rückfälle bei Befundfotos melden; die Projektprüfung erkennt Temp-Pfade.

**Welle 2: Doppelpflege und Regeln an einer Stelle**
6. A2: den QGIS-Unterschied beheben, danach je gemeinsamem Ablauf eine geteilte Hilfe
   (Haltungs-/Schachtseite), Ablauf für Ablauf.
7. B4–B6: Klongruppen zusammenführen.
8. A3 (nach E2), A4, A5: Meterregel, Feldleser, Junction-Schutz je an eine Stelle.
9. R7: Wächter verlangt eine echte Begründung statt Floskeln; R5 Schattenauswertung.

**Welle 3: Struktur (nach Abschluss von Codex' Player- und ServiceProvider-Arbeit)**
10. A6 KI-Dienste zentral, A7 Projekt-Laden aus dem `ShellViewModel` lösen,
    A8 `UI/Ai`-Kandidaten (zuerst die Quelltext-Tests durch Verhaltenstests ersetzen).
11. B2/B7 `AppSettings`- und WebGIS-Brennpunkte, R4/R6 Abbruch und Training Center,
    T4–T6 Testhilfen und Testnetz.

Abnahme je Paket: zuerst ein roter Test, keine Verhaltensänderung ausser der benannten,
Release-Build ohne Warnungen, alle vier Testprojekte grün, GitHub-Prüfung grün, Sperrklinke
nachgezogen.
