# WebGIS-Schreibweg: Prüfung und Härtung (22.09.2026)

Geprüft wurde der am 21.09.2026 gebaute direkte Schreibweg SewerStudio → GEONIS-Attributeditor
(`src/**/WebGis`, Branch `feature/webgis-uebertragung`). Massstab: Der Weg schreibt in den
produktiven Abwasserkataster. Ein falscher oder geleerter Wert ist dort schlimmer als ein nicht
geschriebener; ein falsches «OK» im Log ist schlimmer als ein sichtbarer Fehlschlag.

Ausgangslage vor der Prüfung: Code nie kompiliert, Tests nie ausgeführt (laut Übergabe).
Gemessen: `dotnet build AuswertungPro.sln` 0 Fehler; WebGIS-Tests 60 grün, 1 rot.

## Im echten Lauf belegt (Bürglen, `__WebGIS_Export`)

| Nr | Befund | Beleg | Stand |
|---|---|---|---|
| A1 | Konfliktschutz vergleicht Klartext mit Schlüssel — jedes Objekt mit Auswahl-Handwert wird gesperrt, samt seiner Text- und Zustandsfelder | Lauf 21.09. 15:57: 8 Objekte «Feld Material wurde im WebGIS seit dem Plan geändert (jetzt '144')», im WebGIS unverändert | **behoben** — `Alt` ist der Schlüssel, `AltText` der Klartext (`WebGisFeldAenderung`); Wächter `WebGisExportUseCaseTests.Unveraenderter_stand_eines_auswahlfelds_ist_kein_konflikt` |
| A2 | «Beton, Fertigteil» (Code 104) an Schacht 80461 gefunden, an 525145/59723 «nicht im Katalog» — die Material-Detail-Liste ist gruppenabhängig, die Maske liefert nur die Liste der gerade gesetzten Gruppe | Inventur v2 Zeile 76 (80461, Gruppe Beton) gegen Vorschau 22.09. (59723) | **behoben** — `WebGisExportUseCase.ErgaenzeGruppenKatalogeAsync` lädt die Liste der Zielgruppe über `getControlValues?refid=…&filter=<Gruppe>` (derselbe Aufruf wie Verfahren/Art); `WebGisLesestand.KatalogeNachGruppe`. Erster Lauf im Programm (22.09.): der Server antwortete «Could not load form's default form.» — die Schachtmaske hat einen **Subtyp** (Bauwerksart), ohne den er kein Formular wählt. Jetzt wird der Subtyp aus `data.subtype` der gelesenen Maske mitgegeben (`WebGisLesestand.Subtyp`, wie `art:4` bei der Sanierungsmaske); ein Serverfehler beim Nachladen ist ein Hinweis am Feld, kein Abbruch. Ob der Server die Liste damit liefert, zeigt der nächste Probelauf — schreiben kann der Weg so oder so nichts Falsches. |
| A2b | Jeder faultstring galt als «Sitzung abgelaufen» und beendete die ganze Prüfung — ohne Bericht (22.09. im Programm: leeres Fenster, «Fehler: Could not load form's default form.») | Screenshot Pascal 22.09. | **behoben** — `IstSitzungsfehler` (Token/abgelaufen/Authentifizierung/Session/Login) entscheidet; andere faultstrings sind `WebGisAntwortException` → «Lesefehler» am Objekt bzw. Hinweis am Feld, der Lauf geht weiter. Wächter `Serverfehler_ohne_sitzungsbezug_ist_kein_sitzungsfehler`, `Serverfehler_beim_nachladen_sperrt_nicht_sondern_meldet` |

## Im Code belegt und behoben

| Nr | Befund | Fix | Wächter |
|---|---|---|---|
| B1 | `AntwortAuswerten` endete bei Nicht-JSON, Nicht-Objekt oder `{}` in `Ok()` — Log und Toast melden Erfolg ohne Schreibnachweis | Erfolg nur mit `isFailure`, `newId` oder `message` des Servers; alles andere «nicht als geschrieben gewertet» | `SchreibeAsync_meldet_keinen_erfolg_ohne_erfolgsnachweis_des_servers` (6 Fälle) |
| B2 | Geplante Felder ohne Komponente in der Maske fielen still weg, das Objekt galt als geschrieben | Anzahl gesendeter Komponenten muss der Planzahl entsprechen, sonst kein POST | `SchreibeAsync_bricht_ab_wenn_ein_geplantes_feld_keine_komponente_hat` |
| B3 | Sitzungsprüfung nur an 2 von 5 Aufrufen; HTML statt JSON bei der Suche → «Lesefehler» je Objekt statt «neu anmelden» | `LiesJsonOderSitzungsfehler` an allen Antworten (faultstring oder kein JSON → `WebGisSitzungException`) | drei `LeseAsync_…_wirft_sitzungsfehler`-Tests, `LeseSanierungKatalogAsync_abgelaufene_sitzung_beim_nachladen_wirft` |
| B4 | Fehlte die artabhängige Verfahrensliste, wurde still in der ungefilterten geraten — dieselbe Nummer bedeutet je Art etwas anderes | Mit Filter zählt nur die gefilterte Liste; ein halb geladener Katalog ist kein Katalog (alle Massnahmen gesperrt) | `WebGisKatalogFallbackTests`, `LeseSanierungKatalogAsync_liefert_null_wenn_eine_verfahrensliste_nicht_ladbar_ist` |
| C1 | Kein `try/catch` je Objekt; nach einem Abbruch weder Bericht noch Log über bereits geschriebene Objekte | `GeschuetztAsync` je Objekt/Massnahme; Meldung sofort (`nachObjekt`/`nachMassnahme`); Log je Schritt (`LogStart`/`LogZeile`/`LogAbschluss`/`LogAbbruch`); Bericht `Ergebnis-abgebrochen` | `Ein_schreibfehler_stoppt_die_uebrigen_objekte_nicht`, `Abgelaufene_sitzung_bricht_ab_meldet_aber_die_betroffene_position_vorher`, `WebGisAblaufBerichtTests` |
| C2 | «Jetzt schreiben» baute einen neuen Plan und schrieb ihn ohne Vorschau — im nicht-modalen Fenster gehen Korrekturen dazwischen ungesehen hinaus | `WebGisPlanVergleich.Gleich(bestätigt, frisch)`; weicht der frische Plan ab, wird die neue Vorschau gezeigt statt geschrieben | fünf `…_plan`-Tests in `WebGisAblaufBerichtTests` |
| C4 | Fensterkopf zählte fehlgeschlagene Objekte als «geschrieben» (15:57: «13», es waren 5); Toast immer grün | `WebGisUebersicht.Geschrieben`/`IstErgebnis`; Kacheln im Ergebnis ehrlich; `Warning`-Toast bei Fehlschlägen | `Ergebniskopf_zaehlt_nur_wirklich_geschriebene_objekte` |
| C3 (Teil) | Log-Ordner aus `LastProjectPath` zum Zeitpunkt des Schreibens | Ordner und Benutzer werden VOR dem Lauf gebunden | — (Verdrahtung im ViewModel) |
| E1 | Roter Test `Felder_mit_eigener_regel_laufen_nicht_ueber_die_handwerte`: Stand schon auf Z3, Änderung auf Z3 erwartet — Testfehler | Eingabe Z4 gegen Stand Z3 | derselbe Test, jetzt grün |
| — | `GlobalIdAusResults`-Regex nur für `%7b`/`%7d` klein | `RegexOptions.IgnoreCase` | `LeseAsync_findet_die_globalid_auch_wenn_der_server_die_klammern_gross_kodiert` |
| A4 | **Der Server meldet «gespeichert» für ein Feld, das er gar nicht setzen kann.** Schacht 60284/60105/60106: `Tiefe [m] – → 3.22 \| OK` am 21.09. 15:57, am 22.09. 13:31 nochmals — und im Plan vom 22.09. 14:55 wieder `– → 3.22`. Dreimal OK im Log, das Feld blieb jedes Mal leer. Ursache (Entscheid/Erklärung Pascal): Das WebGIS rechnet die Tiefe aus Sohlen- und Deckelkote; fehlen sie, nimmt es den Wert an und verwirft ihn. Zum Vergleich Schacht 525145: dort angekommen. | **behoben** — `PruefeNachAsync` liest nach jedem Schreibvorgang zurück und vergleicht jedes geschriebene Feld (mit derselben Zahlentoleranz wie der Planbau). Nicht übernommene Felder werden namentlich gemeldet statt als OK gebucht; scheitert die Nachkontrolle selbst, bleibt es beim Erfolg mit Hinweis «nicht nachgeprüft». Kostet einen Lesevorgang je geschriebenem Objekt. Entscheid Pascal 22.09.: Tiefe weiter ins Feld schreiben und bei Misserfolg melden — die Bemerkung trägt er von Hand nach. | `Ein_feld_das_der_server_still_verwirft_wird_gemeldet_statt_als_ok`, `Angekommener_wert_bleibt_ein_erfolg`, `Nachkontrolle_akzeptiert_dieselbe_zahl_in_anderer_schreibweise`, `Nachkontrolle_die_selbst_scheitert_macht_aus_einem_erfolg_keinen_fehler` |
| A3 | Zahlen-Textfelder wurden zeichengenau verglichen: geschrieben `1.80`, WebGIS speichert `1.8` → bei **jedem** Lauf dieselbe Scheinänderung und ein unnötiger Schreibvorgang (Probelauf 22.09., Schacht 525145 «Tiefe [m]: 1.8 → 1.80») | **behoben** — `GleicherText` vergleicht numerisch, wenn beide Seiten Zahlen sind (Tiefe, Sohlen-/Geländehöhe, Rotation); sonst weiter zeichengenau | `Gleiche_zahl_in_anderer_schreibweise_ist_keine_aenderung` (5 Fälle), `Andere_zahl_bleibt_eine_aenderung`, `Text_der_keine_zahl_ist_wird_weiterhin_zeichengenau_verglichen` |

Stand nach der Härtung: 103 WebGIS-Tests grün (vorher 61, davon 1 rot); `AuswertungPro.sln` 0 Fehler;
UI-Wächter (Registrierung, Design, Architektur) grün.

## Offen — nicht Teil dieser Härtung

- **E2 Breite/Höhe der Haltung.** `Lichte_Hoehe_mm` existiert als SewerStudio-Feld nicht → die Höhe
  wird nie geschrieben; `DN_mm` (fachlich die Höhe) geht in «Breite [mm]». Inventur v2 nennt
  `d06f8d1f` in Abschnitt 1 «Breite», in 2b «Höhe»; an einem runden Profil (300/300) kann die Probe
  das nicht unterscheiden. **An einem Eiprofil live klären, bevor Haltungsmasse geschrieben werden.**
- **C3 Rest.** Kein `IShellOperationGuard` (Projektwechsel/Schliessen während des Laufs erlaubt);
  kein `ProjectWritePathGuard` am Log-Ordner; `BauePlanAsync` iteriert die lebende Projektliste
  über Netz-Awaits hinweg (Neu/Löschen im offenen Fenster → «collection modified»).
- **D1 Abmelden.** Beendet keine WebOffice-Sitzung; das persistente Browserprofil unter
  `%LOCALAPPDATA%\SewerStudio\WebGisBrowser` behält die ADFS-Cookies, der `CookieContainer` wird
  nicht geleert. Entscheid nötig: bewusst so (dokumentieren) oder serverseitig ausloggen und Profil
  leeren.
- **D2 Benutzerkontext.** Wird aus jeder Browser-Anfrage ohne Host-Prüfung gelesen und beim
  nächsten Mal ungeprüft aus den Einstellungen übernommen.
- **D3 Chromium-Nachinstallation** liest nur stderr — kann bei vollem stdout-Puffer hängen (nur
  ohne Edge/Chrome).
- **E3 Handwerte.** Zwei Handwerte auf eine refId: der zweite fällt ohne Hinweis weg (Reihenfolge aus
  dem `FieldMeta`-Dictionary). Hand-«Sanierungsbedarf» und Schacht-«Baujahr» werden still
  übersprungen. «Saniert» ohne Zustandsklasse setzt den Sanierungsbedarf ohne Hinweis.
- Doppelprüfung der Massnahmen ignoriert Jahr und Umfang (zwei Reparaturen 2020/2026 gelten als
  eine). Zustand/Sanierungsbedarf schreiben feste Codes ohne Blick in den gelesenen Katalog.
- Trigonet-Abstimmung, Beats Freigabe, Datenlücken Bürglen (siehe Übergabe vom 21.09.).

## Verworfen (gemeldet, hält nicht stand)

Maskenvorgaben (`keySelected`) beim Anlegen mitgeschrieben — der Planer schliesst den Fall aus.
Cookie-Übernahme (`TrimStart('.')`, verschluckte `CookieException`) — die Läufe belegen, dass die
Sitzung trägt. Timeout während `saveData` — der nächste Lauf liest frisch bzw. erkennt die Dublette.
`SanierungsZeilen` mit festen Spaltenindizes — am Schacht live belegt (66922). `playwright.ps1` mit
Bypass — derselbe Weg wie beim PDF-Export.

## Abnahme

- Build: `dotnet build AuswertungPro.sln` — 0 Fehler (bei laufendem SewerStudio scheitert nur das
  Kopieren nach `bin\Debug`; dann `-o .tmp/testout-webgis` bauen).
- Tests: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter FullyQualifiedName~WebGis`
  — 103 grün.
- Nächster Echtlauf: SewerStudio schliessen, `bin\Debug` neu bauen, dann Probelauf an Bürglen.
  Erwartet werden 0 Konfliktsperren aus Klartext/Schlüssel und Material «Beton, Fertigteil» an
  525145/59723/60284 als Änderung statt Hinweis. Bleibt es beim Hinweis «Liste der Gruppe konnte
  nicht nachgeladen werden», ist der `getControlValues`-Aufruf der Schachtmaske einmal im Browser
  mitzuschneiden (Material-Gruppe in der Maske umstellen und den Aufruf abgreifen) — der Code
  bleibt bis dahin beim Hinweis und schreibt nichts Falsches.
