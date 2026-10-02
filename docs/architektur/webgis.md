# WebGIS (GEONIS): Senden und Holen

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- WebGIS-Export: Zustand + Sanierung nach GEONIS (21.09.2026, erste Stufe)

## WebGIS-Export: Zustand + Sanierung nach GEONIS (21.09.2026, erste Stufe)

Neuer Weg SewerStudio -> WebGIS (GEONIS Attribute Editor, WebOffice) fuer die
Sanierungsabnahme. Gegenrichtung zum bestehenden GeoShop-/Katasterimport.

- `Application/WebGis`: reine Verträge und Regeln, WPF-frei.
  - `WebGisFeldkarte` — refIds und Code-Tabellen, maschinell aus den echten Masken
    awk_haltung / awk_abwasserknoten erhoben (Beleg im Kundenprojekt unter
    `__WebGIS_Export/Feldzuordnung_SewerStudio_WebGIS_v2.md`). Zustand Z0..Z4 = 100..104,
    Sanierungsbedarf „Saniert" = 106. Sanierungsbedarf-refIds sind die 2. Combo des
    Paars (Haltung `2b200c69-4a70-…`, Schacht `ae898ff7-8b6d-…`).
  - `WebGisSaniertKriterium` — „saniert" gilt NUR bei einer ausgefuehrten
    Sanierungs-Objektakte (Art `sanierung`, `sanierung.s_status`=„Ausgeführt"), NIE
    aus dem Bemerkungstext. (Bürglen: 10 Haltungen, 25 Schaechte mit Akte; 9 Haltungen
    tragen „Saniert…"-Bemerkung OHNE Akte — die bekommen keinen Sanierungsbedarf, nur Hinweis.)
  - `WebGisBemerkung` — führt WebGIS- und SewerStudio-Bemerkung zusammen, nie überschreiben.
  - `WebGisExportPlanBuilder` — reine Regel je Objekt: Zustand aus Zustandsklasse,
    Sanierungsbedarf=Saniert nur bei Akte, Bemerkung-Merge, Baujahr nur wenn WebGIS leer,
    **kein Laengenfeld je**. Ohne WebGIS-Treffer -> gesperrt.
  - `WebGisExportUseCase` — liest je Objekt frisch, baut den Plan; `FuehreAusAsync`
    schreibt nur bei `probelauf:false` und prueft vor jedem Schreiben den Ausgangswert
    (Konfliktschutz), sonst Sperre.
- `Infrastructure/WebGis`: `GeonisWebGisClient` (IGeonisWebGisClient) spricht die
  interne WebOffice-Schnittstelle an — Suche (synserver GET_QUERY_FULL_TEXT ->
  GET_RESULTS für die GlobalID), `getLayoutDataCombined` (lesen), `saveData` (nur die
  geänderten Komponenten; Combo als `keySelected`, EditBox als `value`; Geometrie raus).
  HttpClient injiziert. `WebGisZugang`/`IWebGisZugangQuelle`: die angemeldete Sitzung
  (JSESSIONID + X-syn-Kontext + synserver session_id) kommt aus einem eingebetteten
  WebView2 — bewusst KEIN nachgebauter HTTP-/ADFS-Login. Ohne Sitzung liefert der
  Client nichts (alle Positionen gesperrt).
- DI: `ServiceProvider.WebGis.cs`, Registrierung 169 -> 170 (`ServiceProviderRegistrationMap`,
  `ServiceProviderRegistrationTests`).
- Tests: `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/` — Feldkarte/Bemerkung/
  Kriterium/PlanBuilder (reine Regeln), `WebGisSanierungRegelnTests` (Katalog je Art,
  Jahr->Datum, Sperren, Doppel) und `GeonisWebGisClientTests` (Suche-Parsing, Lesen inkl.
  Sanierungsliste, saveData-Payload fuer Objekt und Massnahme gegen Fake-HttpMessageHandler).
- Stufe 2 (21.09.2026): Sanierungsmassnahmen anlegen. `WebGisSanierungFeldkarte`
  (Tabelle `AWZ_UNTERHALT`, Subtyp `art:4` konstant wie im Browser, refIds der Maske
  „Sanierungsmassnahme", Relation je Objektart `sew_awk_haltung_awz_unterhalt` /
  `sew_awk_abwasserknoten_awz_unterhalt`, Listen-refIds in den Elternmasken),
  `WebGisSanierungKatalog` (Combo-Listen live aus `getEmptyData`; Verfahren ist von der
  Art abhaengig und kommt je Art aus `getControlValues?refid=<Verfahren>&filter=<Art>` —
  Reparatur: Vermoertelung=33, Renovierung: Schlauchverfahren=27), `WebGisSanierungPlanBuilder`
  (Klartext -> Schluessel NUR ueber den Katalog, LokalerEintrag ist ein anderer
  Nummernkreis; fehlender Katalogwert oder fehlende Art = gesperrt; Jahr -> 01.01.JJJJ;
  gleiche Art/Status/Verfahren schon in der Liste = nicht doppelt, ausser das Jahr ist nachweislich ein
  anderes, siehe «Massnahmen-Doppel» unten). `WebGisLesestand.Sanierungen`
  traegt die vorhandenen Massnahmen aus der GListBox (`values`: Beginn, Art, Status,
  Verfahren, GlobalId). `GeonisWebGisClient.Sanierung.cs` (partial): `LeseSanierungKatalogAsync`,
  `ErstelleSanierungAsync` (Payload wie mitgeschnitten: alle Komponenten `{value,refId,
  missingValue}`, Combo-Wert als Schluessel-Text, Datum ISO-UTC, Kopf mit relation/
  relationKeyField=globalid/relationId=Eltern-GlobalID; Antwort `{newId,isFailure,message}`).
  `WebGisExportUseCase` plant je ausgefuehrter Akte eine Massnahme (Katalog einmal je
  Objektart) und legt sie nach den Objektschreibungen an (frisch lesen, Doppel-Check).
  Live belegt: 80480-80478 -> 66921 (Renovierung), Schacht 80478 -> 66922 (Reparatur).
  Vorschau Bürglen: `__WebGIS_Export/Probelauf-Vorschau_Sanierungen_20260921.txt`
  (32 anzulegen, 3 gesperrt: 2 schon vorhanden, Haltung 80462-80461 ohne Art).
- Stufe 3 (21.09.2026): Anmeldung + Oberflaeche. `Infrastructure/WebGis/PlaywrightWebGisAnmeldung`
  (IWebGisZugangQuelle): sichtbares Browserfenster ueber Playwright, der Benutzer meldet sich
  selbst an — SewerStudio sieht kein Passwort. Browserwahl in dieser Reihenfolge: **msedge**
  (auf jedem Windows da, kein Download), **chrome**, sonst Playwright-Chromium (das laedt
  einmalig ~150 MB nach %LOCALAPPDATA%\ms-playwright, wie beim PDF-Export; wird bei Bedarf
  automatisch ueber playwright.ps1 nachinstalliert). `LaunchPersistentContextAsync` mit eigenem
  Profil unter %LOCALAPPDATA%\SewerStudio\WebGisBrowser — nicht das Alltagsprofil des
  Benutzers, aber Cookies bleiben ueber Programmstarts erhalten. Gelesen werden `scriptAPI.Env()` (jsessionid, sessionid, isumnameduser),
  die Cookies (in den HttpClient-CookieContainer) und aus dem ersten Attributeditor-Aufruf
  der X-syn-Kontext (`KontextAusUrl`); der Kontext wird in AppSettings gemerkt
  (`WebGisSynLogin/Roles/Groups`), danach genuegt die Anmeldung. Rollenwert woertlich
  "WebOffice+-+Editing" (Plus gehoert zum Wert, geht als %2B). `WebGisExportBericht`
  (Application) fuellt das bestehende XTF-Vorschaufenster (Alt/Neu, Warnungen, Details)
  und den Ergebnistext. Export-Seite: Karte „Direkt ins WebGIS übertragen" mit
  Anmelden / Pruefen und schreiben / Abmelden (`ExportPageViewModel.WebGis.cs`, nur im
  ServiceProvider-Konstruktor aktiv); Berichte unter `<Projekt>\__WebGIS_Export\WebGIS_*.txt`.
  Registrierung 170 -> 171 (IWebGisZugangQuelle).
  `WebGisUebersicht` (Application) ist die Anzeige-Sicht: NACH OBJEKT gruppiert (eine Karte je
  Haltung/Schacht mit ihren Feldzeilen alt -> neu, Hinweisen und Sperren), unveraenderte Objekte
  fallen weg, und Gleichartiges wird zu einer Sammelzeile gebuendelt ("43 Massnahmen bereits
  vorhanden", "n Objekte ohne Sanierungs-Akte") — vorher standen 48 Einzelwarnungen untereinander.
  Kopf: vier Zahlen (geaendert / neue Massnahmen / gesperrt / nur Hinweis).
  Pruef-/Schreibfenster `WebGisVorschauWindow` ist NICHT modal (Wunsch Pascal): bleibt offen,
  waehrend in Haltungen/Schaechten korrigiert wird; "Neu pruefen" baut den Plan aus dem
  aktuellen Projektstand, "Jetzt schreiben" prueft immer erst frisch und schreibt dann
  denselben Plan; Ergebnis erscheint im selben Fenster. Waehrend Lesen/Schreiben nicht schliessbar.
- Stufe 4 (21.09.2026): HANDWERTE. Alles, was in SewerStudio von Hand gesetzt ist
  (`FieldMeta.UserEdited`), geht mit, sofern `WebGisHandwertKarte` das Feld kennt. ALLE 26
  refIds dort sind live an den Masken gegen den sichtbaren Wert geprueft (Schacht 80461,
  Haltung 80480-80478) — drei erschlossene refIds waren dabei FALSCH (824e25e3, 14998fd3,
  d06f8d1f), also nie eine refId aus der Paar-Heuristik uebernehmen, immer am Objekt pruefen.
  Schacht: Funktion, Nutzungsart, Material(+Detail), Form, Breite/Laenge, 2. Mass, Status,
  Funktion hier., Tiefe, Sohlen-/Gelaendehoehe, Rotation, Ebene, Lagebestimmung.
  Haltung: Material(+Detail), Profiltyp, Breite, Hoehe, Nutzungsart, Funktion hier./hydr.,
  Status, Verbindungsart, Lagebestimmung.
  Combo-Werte NUR ueber den Klartext gegen `WebGisLesestand.Kataloge` (keys/values der
  gelesenen Komponenten), gefaltet ueber Gross/Klein, Unterstrich, ae/oe/ue und Kuerzel in
  Klammern; zusaetzlich zaehlt der Teil nach dem Punkt ("PAA.Sammelkanal" -> Sammelkanal).
  Kein Treffer ⇒ Feld nicht geschrieben + Hinweis (Objekt bleibt offen), z.B. Rohrmaterial
  "Zement" fehlt im Katalog. Zwei SewerStudio-Felder auf dieselbe refId (DN/lichte Breite):
  erster Treffer zaehlt. Leerer Handwert loescht nie.
  NICHT ueber die Handwerte: Zustand/Sanierungsbedarf/Bemerkung/Laenge (eigene Regeln),
  BAUJAHR (nie ueberschreiben, nur fuellen wenn leer) und EIGENTUEMER/BETREIBER (fuehrt das
  WebGIS) — beide nur melden. Projektinterne Felder (NR., Link, PDF_Path, Strasse, VSA-Noten,
  Kennungen, Kosten) werden still uebergangen, damit der Bericht lesbar bleibt.
- LEHRE 21.09.2026: saveData uebernimmt bei Combos NUR "value" (Schluessel als Text);
  "keySelected" wird ignoriert, value:null LEERT das Feld. Erster Lauf hat so bei 44 Objekten
  Zustand/Sanierungsbedarf geleert; `SchreibeAsync` sendet seither {value, refId, missingValue}
  wie der Browser. Zweiter Lauf stellt die Sollwerte wieder her.
- PRUEFUNG UND HAERTUNG 22.09.2026 (`docs/audits/2026-09-22-webgis/BEHEBUNG.md`). Build 0 Fehler,
  103 WebGIS-Tests gruen. Zwei Befunde standen schon im Buerglen-Log, sechs weitere im Code:
  - **`WebGisFeldAenderung.Alt` ist der SCHLUESSEL, `AltText` der Klartext.** Der Konfliktschutz
    (`SchreibeEineAsync`) vergleicht Schluessel gegen Schluessel. Vorher stand bei Combos der
    Klartext in `Alt`: Lauf 15:57 sperrte 8 Objekte mit «seit dem Plan geaendert (jetzt '144')»,
    obwohl im WebGIS nichts passiert war — und mit dem Objekt auch Zustand, Tiefe, Form.
  - **Abhaengige Detail-Listen sind gruppenabhaengig** (Material-Detail wie Verfahren/Art): Die
    Maske liefert nur die Liste der GERADE gesetzten Gruppe. «Beton, Fertigteil» = 104 wurde an
    80461 (Gruppe Beton) gefunden, an 525145/59723 nicht. `ErgaenzeGruppenKatalogeAsync` laedt die
    Liste der Zielgruppe ueber `getControlValues?refid=…&filter=<Gruppe>` nach
    (`WebGisLesestand.KatalogeNachGruppe`); ohne Liste bleibt der Hinweis, nie ein geratener Wert.
    **Die Objektmasken haben einen Subtyp** (Schacht: Bauwerksart): Ohne `subtype=<name>:<wert>`
    antwortet der Server «Could not load form's default form.» (22.09. im Programm). Der Subtyp
    kommt aus `data.subtype` der gelesenen Maske (`WebGisLesestand.Subtyp`) und geht beim
    Nachladen mit; ob der Server damit liefert, ist noch nicht belegt — sonst Aufruf mitschneiden.
  - **Nicht jeder faultstring ist die Sitzung.** `IstSitzungsfehler` (Token/abgelaufen/
    Authentifizierung/Session/Login) beendet den Lauf; jeder andere faultstring ist eine
    `WebGisAntwortException` fuer genau diesen Aufruf — «Lesefehler» am Objekt, Hinweis am Feld,
    der Lauf geht weiter. Vorher brach die ganze Pruefung ohne Bericht ab.
  - **Erfolg nur mit Erfolgsnachweis des Servers** (`AntwortAuswerten`: `isFailure`, `newId` oder
    `message`). Kein JSON, kein Objekt, `{}` oder erfundene Formen heissen «nicht als geschrieben
    gewertet» — vorher endete alles davon in `Ok()`, mit OK-Zeile im Log.
  - **Jede Antwort geht durch `LiesJsonOderSitzungsfehler`**: faultstring ODER kein JSON (ADFS-
    Anmeldeseite) ist eine `WebGisSitzungException` — auch bei Suche und `getControlValues`, die
    vorher gar nicht prueften (52x «Lesefehler» statt «neu anmelden»).
  - **Ein geplantes Feld ohne Komponente in der Maske sperrt das Objekt** statt still wegzufallen.
  - **Mit Filter zaehlt nur die gefilterte Liste** (`WebGisSanierungKatalog.Schluessel`); ein halb
    geladener Sanierungskatalog ist keiner (alle Massnahmen gesperrt, nie ein Verfahren geraten).
  - **Log je Schritt, nicht am Ende.** `FuehreAusAsync` schuetzt jedes Objekt (`GeschuetztAsync`),
    meldet es sofort (`nachObjekt`/`nachMassnahme`), und das ViewModel haengt `LogZeile` sofort
    an; Sitzung/Abbruch schreiben `LogAbbruch` und den Bericht `Ergebnis-abgebrochen`. Ordner und
    Benutzer werden VOR dem Lauf gebunden.
  - **«Jetzt schreiben» schreibt nur den bestaetigten Plan.** `WebGisPlanVergleich.Gleich`
    vergleicht den frischen Plan mit dem zuletzt gezeigten; weicht er ab (Korrektur im nicht-
    modalen Fenster), erscheint die neue Vorschau statt eines Schreibvorgangs.
  - Ergebniskopf und Kacheln zaehlen `Geschrieben`, nicht «Objekte mit Aenderung» (15:57 stand
    «13 geschrieben», es waren 5); bei Fehlschlaegen ein Warn-Toast, kein gruener.
  - Der rote Test `Felder_mit_eigener_regel…` war ein Testfehler (Stand schon auf Z3).
  - OFFEN: Breite/Hoehe der Haltung (`Lichte_Hoehe_mm` existiert nicht, `DN_mm` geht auf Breite;
    Inventur v2 nennt d06f8d1f einmal Breite, einmal Hoehe — an einem Eiprofil klaeren), Abmelden beendet keine
    Sitzung (Profil behaelt ADFS-Cookies).
  - E3 ERLEDIGT (24.09.2026): **Was nicht ins WebGIS geht, sagt ein Hinweis** (`WebGisExportPlanBuilder`): zwei
    verschiedene Handwerte auf dieselbe refId (DN_mm/Lichte_Breite_mm -> «Breite [mm]», gleich nach Zahlwert ist
    kein Hinweis), ein Sanierungsbedarf von Hand (geht nie hinaus; mit Akte setzt sie «Saniert»), «Saniert» ohne
    Zustandsklasse (Zustand im WebGIS bleibt). Das Schacht-Baujahr fuellt seit 23.09. ein leeres Feld. Tests
    `WebGisHandwertHinweisTests`.
  - MASSNAHMEN-DOPPEL MIT JAHR (24.09.2026): **`WebGisMassnahmenVergleich` ist die EINE Regel** fuer Senden-Plan,
    Nachpruefung vor dem Anlegen (`LegeEineAnAsync`) und Holen (`SchonVorhanden`, `LegeAn`): gleiche Art/Status/
    Verfahren (gefaltet wie beim Holen) UND Jahr nicht nachweislich verschieden. Zwei BEKANNTE, verschiedene Jahre
    = zwei Massnahmen (Reparatur 2020 und 2026; der Plan nennt die aeltere als Hinweis). Fehlt ein Jahr auf einer
    Seite, bleibt es «bereits vorhanden». Das Jahr der WebGIS-Liste kommt aus deren erster Spalte «Beginn»
    (`WebGisSanierungZeile.Beginn/Jahr`, `WebGisSanierungFeldkarte.JahrAusDatum`: «01.01.2026», ISO, ms, «2026»);
    LIVE NICHT BELEGT, dass «Beginn» das Sanierungsjahr der Maske ist — das Fixture hat dort null. Der Umfang
    steht nicht in der Liste und zaehlt deshalb nicht. Tests `WebGisMassnahmenJahrTests`,
    `WebGisExportUseCaseTests.Vor_dem_anlegen_zaehlt_das_jahr…`.
  - D2/D3 ERLEDIGT (24.09.2026): Der X-syn-Kontext zaehlt nur aus einer HTTPS-Anfrage an genau den Host der
    WebGIS-Adresse (`KontextAusUrl(url, basisUrl)`; der Browser fragt auch fremde Server an), und nur plausibel
    (`WebGisSynKontext.IstPlausibel`: Login/Gruppen gefuellt, keine Steuerzeichen — die Werte gehen als Kopfzeilen
    hinaus). Der gemerkte Kontext kommt ueber `WebGisSynKontext.AusEinstellungen` und wird genauso geprueft. Die
    Chromium-Nachinstallation laeuft ueber `ExternalProcessRunner` (beide Ausgaben, 10 Minuten Zeitlimit) statt
    einer eigenen Kopie, die nur die Fehlerausgabe las und bei vollem Puffer ewig wartete.
  - C3 ERLEDIGT (24.09.2026): **Beide Planer bauen ihr Abbild VOR dem ersten Netzaufruf** (Eingaben aller
    Haltungen/Schaechte, `WebGisAktenAbbild.Sanierungen`); danach laufen sie auf einem Netzthread weiter, waehrend
    im nicht-modalen Fenster Datensaetze entstehen oder verschwinden — vorher «Collection was modified». **Das Holen
    schreibt nur auf dem Thread des Aufrufers**: `PruefeVorUebernahmeAsync` liest nach, `Uebernimm` schreibt danach
    (`UebernimmGeprueftAsync` mit `ConfigureAwait(true)`); `WebGisHolenAblauf` prueft «Projekt gewechselt» direkt
    davor, nicht mehr danach. **Der Schreiblauf ins WebGIS haelt die Projektsperre der Export-Seite**
    (`TryBeginProjectOperation`: kein Projektwechsel, kein Schliessen), Lesen/Pruefen bewusst nicht (dort wird im
    offenen Fenster korrigiert). **Berichte und Log gehen durch `WebGisBerichtAblage`** (Infrastructure, eine Stelle
    fuer Senden und Holen, `ProjectWritePathGuard`); ohne sicheren Berichtsordner schreibt das Senden nichts, weil
    das Log der Beleg ist. Tests `WebGisImportUseCaseTests` (Eine_neue_haltung_…, Uebernahme_schreibt_auf_dem_thread_…
    mit Einzel-Thread-Kontext), `WebGisExportUseCaseTests.Eine_neue_haltung_…`, `WebGisBerichtAblageTests` (Junction).
- Stufe 5 (23.09.2026): MATERIALGRUPPE BEIM SENDEN und HOLEN WEBGIS -> SEWERSTUDIO.
  - Materialgruppe: Nennt der Materialtext keine Gruppe («Polypropylen»), laedt
    `ErgaenzeGruppenKatalogeAsync` die Detail-Listen ALLER Gruppen; `WebGisExportPlanBuilder`
    setzt Gruppe + Detail nur, wenn GENAU eine Gruppe den Wert fuehrt (mehrere -> Hinweis).
    Wie im Browser: erst Gruppe, dann Detail. Live noch nicht belegt, dass saveData beides in
    einem Aufruf annimmt — das Zuruecklesen nach dem Schreiben meldet es sonst.
  - ENTSCHEID PASCAL 23.09.2026 (ersetzt «Laenge immer aus dem WebGIS» vom 21.09.; praezisiert abends): Die
    HALTUNGSLAENGE in SewerStudio ist die des Operateurs und geht NIE ins WebGIS. EIGENTUM/BETREIBER aendert
    das Programm im WebGIS NIE. Alle drei werden seit 23.09. abends GEHOLT, «rein informativ, der
    Vollstaendigkeit halber»: nur in LEERE Felder, nie ueber einen vorhandenen Wert (Kanalfirma, GeoShop, Hand)
    — Laenge geometrisch (`HaltungLaengeGeomRef`, zwei Stellen) nach `Haltungslaenge_m`, Eigentuemer als
    WebGIS-Klartext nach `Eigentuemer`, Betreiber in die Wurzelakte (`haltung.operator`/`schacht.betreiber`,
    Listeneintrag ueber den WebGIS-Schluessel = Originalcode, sonst Klartext). Die Eigentuemer-/Betreiber-
    refIds sind nur aus der Inventur: Das Feld zaehlt nur, wenn seine Liste den Organisationsschluessel «Bund»
    fuehrt (`WebGisFeldkarte.OrganisationBundKey`), sonst Hinweis. Tests `WebGisImportPlanBuilderTests`
    (Informativ-Faelle), `WebGisImportUebernahmeTests`.
    **ENTSCHEID PASCAL 24.09.2026 fuer EIGENTUEMER/BETREIBER** («muss perfekt von WebGIS übernommen werden, diese
    Werte ändern sich sehr selten»): Das WebGIS FUEHRT beide. Der WebGIS-Wert ersetzt beim Holen jeden vorhandenen
    Wert zeichengenau — GeoShop wie Kanalfirma (`WebGisImportPlanBuilder.PlaneFuehrungswert`; bei der Uebernahme
    `DarfErsetzen`). SEIT 24.09.2026 ABENDS (Entscheid Pascal «Eigentuemer und Betreiber duerfen vom WebGIS
    ueberschrieben werden») weicht auch eine Handeingabe, bewusst leer eingeschlossen: `WebGisFuehrungsfelder.HolenUeberschreibtHand`
    laesst Eigentuemer ueber die Handmarke, `GibHandmarkeFrei` nimmt sie vor dem Schreiben weg (scheitert das Schreiben,
    kommt sie zurueck), `SchreibeAkteGruppe` ersetzt den Betreiber auch mit `VonHand`. Danach ist der Wert ein Katasterwert
    ohne Handmarke. Der Konfliktschutz bleibt: Wurde der Wert seit der Vorschau geaendert, bleibt er. Die Haltungslaenge
    bleibt rein informativ. Ins WebGIS geschrieben werden alle
    drei weiterhin nie. ORGANISATIONSTYP «SO WIE ES IM WEBGIS IST» (Entscheid Pascal 24.09.2026): `WebGisOrganisationen`
    (Domain) liest die WebGIS-Organisationsliste aus dem Objektaktenkatalog (`haltung.owner`, Labels «Name (Typ)»;
    live liefert die Haltungsmaske genau so «AWU_von_oeffentlich (Abwasserverband)», die Schachtmaske nur
    «AWU_von_privat»). `EigentumVokabular.NachOrganisationstyp` fragt ZUERST diese Liste (AWU_von_* = Abwasserverband,
    Genossenschaft/Kooperation = Genossenschaft_Korporation, «Unbekannt» faellt auf die bisherige Regel), und
    `Normalisieren`/DSS `Organisation` nehmen den WebGIS-Namen ohne Typzusatz — Haltung und Schacht ergeben EINE
    Organisation «AWU_von_privat». Der Tabellenwert selbst bleibt zeichengenau der WebGIS-Text.
    `OwnershipAwuFilter.IsAwu` (NPK-135-Leistungsverzeichnis) erkennt «AWU_von_…» als AWU — sonst fielen geholte
    AWU-Leitungen still aus dem LV. EXCEL (Entscheid Pascal 24.09.2026): Beide Excel-Vorlagen zaehlen, summieren und
    faerben jeden WebGIS-Namen (bloss und «Name (Typ)») nach seinem WebGIS-Typ in der bestehenden Kategorie
    (Abwasserverband -> Abwasser Uri, Kanton, Bund, Gemeinde, Privat); Genossenschaften/«Unbekannt» bleiben ungefaerbt.
    `ExcelReportStyle.Eigentuemer` und `tools/ExcelVorlagenBauer/vorlage.py` lesen dieselbe Katalogliste; keine
    Schreibweise doppelt (Excel vergleicht ohne Gross/Klein). Neu bauen mit openpyxl 3.1.5/Pillow 11.3.0 (Nachbau
    bis auf docProps/core.xml identisch). Tests `ExcelExportVorlagentreueTests` (Eigentuemerblock beider Vorlagen,
    Export mit Formelauswertung), `ExcelReportStyleTests`.
    Tests `EigentumVokabularTests` (ganze WebGIS-Liste), `OwnershipAwuFilterTests`, `XtfNeuPlanBuilderTests`,
    `XtfDssExportTests` (Webgis_…).
    **SANIERUNGSBEDARF NUR WENN LEER (Entscheid Pascal 24.09.2026):** Das Holen liest den Sanierungsbedarf
    (`WebGisFeldkarte.SanierungsbedarfRef`) fuer Haltung UND Schacht und fuellt ihn ausschliesslich in ein LEERES
    Feld (`WebGisImportPlanBuilder.Sanierungsbedarf`, WebGIS-Begriff zeichengenau ueber `WebGisImportWert`). Jeder
    vorhandene Wert bleibt — auch GeoShop/Kataster, anders als bei den Kartenfeldern. Bewusst leer (Handeingabe)
    bleibt leer mit Hinweis; «Unbekannt» fuellt nichts. Bei der Uebernahme sperrt `SeitVorschauUnveraendert` ein
    inzwischen gesetztes Feld. Zustand und Bemerkung holt das Programm weiterhin nicht.
  - Holen (`WebGisImportUseCase`, schreibt NIE ins WebGIS): Baujahr wenn leer, dazu alle Felder der
    `WebGisHandwertKarte`
    (refIds live geprueft). Entscheid Pascal: WEBGIS VOR GEOSHOP — leere Felder fuellen UND Werte
    mit Herkunft Kataster/Xtf/Xtf405/Ili ohne Handmarke ersetzen (`IstErsetzbar`); Handwerte und
    Protokollwerte nie. Vor dem Ersetzen wird nochmals geprueft, ob der Wert seit der Vorschau
    gleich ist. **ENTSCHEID PASCAL 23.09.2026 abends: Die Daten der Kanalfirmen sind der Ist-Zustand
    (aber nicht vollstaendig) und werden weder vom WebGIS-Holen noch vom GeoShop-Abgleich
    ueberschrieben — nur leere Felder werden ergaenzt; WebGIS darf GeoShop-/QGIS-Werte ersetzen.**
    Umgesetzt: `IstErsetzbar` = nur `FieldSource.Kataster` ohne Handmarke (Xtf/Xtf405/Ili/Legacy vergeben
    auch die Kanalfirmen-Importe); `GeoShopAbgleichPlanBuilder.ImmerAusXtf` ist entfernt (die Laenge wird
    wie jedes Fachfeld nur ergaenzt; im Feldvergleich bleiben Abweichungen abgewaehlt). «Übernehmen» im
    Holen-Fenster laeuft ueber `WebGisImportUseCase.UebernimmGeprueftAsync`: jedes Objekt mit etwas zu
    uebernehmen und jede Massnahme wird vorher nochmals gelesen; weicht der Stand samt Aenderungsdatum von
    der Vorschau ab (`WebGisImportPosition.GelesenerStand`, `WebGisSanierungImport.GelesenerStand`), wird
    genau dieses Objekt nicht uebernommen (Sperre mit Grund), erst pruefen, dann schreiben.
    `WebGisImportWert` bringt den Klartext auf den SewerStudio-Begriff («Sammelkanal»
    -> PAA.Sammelkanal nur wenn das Blatt eindeutig ist, «In Betrieb» -> in_Betrieb); kein Treffer
    = Hinweis, «unbekannt» fuellt nichts. DN nur wenn WebGIS-Breite = Hoehe; Lichte_Breite/Hoehe
    nicht (Breite/Hoehe weiter OFFEN). Haltung kennt kein blosses «Kunststoff» -> Hinweis.
  - Sanierungsmassnahmen: je Zeile der WebGIS-Liste ohne Akte in SewerStudio liest
    `LeseMassnahmeAsync` (getLayoutDataCombined, table AWZ_UNTERHALT, id=GlobalId — live NOCH
    NICHT belegt) die Massnahme; `WebGisSanierungImportRegel` bildet sie auf die Sanierungsakte ab
    (Klartext gegen den Katalog der Akte, Verfahren gegen die Liste der Art, ohne Art keine Akte),
    schreibt wie ein Import (VonHand=false) mit Beleg System «WebGIS» + GlobalId. Doppel: gleiche
    Kennung ODER gleiche Art/Status/Verfahren. Folge: eine geholte ausgefuehrte Massnahme macht
    das Objekt fuer den Export «saniert».
  - Materialgruppe der Objektakte (`haltung.pipegroup` / `schacht.materialgruppe`, KEIN Tabellenfeld):
    das Holen schreibt die WebGIS-Gruppe in die Wurzelakte, wenn leer oder nicht von Hand gesetzt
    (VonHand=false, Format 3). Vorher blieb sie leer, obwohl das Detail gesetzt war.
  - Die WebGIS-GlobalID kommt ausschliesslich aus dem Link eines eindeutigen Suchtreffers mit
    exakt gleichem Namen. Beim bestaetigten Holen wird sie getrennt von `Objekt_ID` in Haltung
    oder Schacht gespeichert. Ein abweichender gespeicherter Wert sperrt den Import und Export.
    Die OBJECTID der Maske und GeoShop-/SIA405-Kennungen dienen nicht als GlobalID.
    Im Holen-Fenster erscheint die Kennung als "GlobalID (WebGIS)" in der Vorschau.
    `WebGisExportUseCase` uebergibt Feldwerte mit Kataster-Herkunft nicht an das WebGIS;
    bestaetigte eigene Werte und Protokollwerte bleiben fuer den Export vorgesehen.
  - WebGIS-Suche: `PlaywrightWebGisAnmeldung` gibt einen Zugang erst mit JSESSIONID UND
    synserver-Session-ID frei. `GeonisWebGisClient` meldet eine fehlende Suchsession,
    HTTP-401/403 und ungueltige Suchantworten als Zugangsfehler statt 182 scheinbar nicht
    gefundene Objekte. Der echte 14:33-Lauf vom 23.09. (182/182 gesperrt, zuvor 10/182)
    belegt einen systemischen Suchausfall, nicht dessen genaue HTTP-Ursache. Neu anmelden
    und erneut pruefen bleibt die Live-Abnahme.
    Wenn alle Namen ohne eindeutigen Treffer bleiben, zeigen Holen und Export einen
    gemeinsamen Suchhinweis im Bericht statt nur einzelne gesperrte Zeilen.
  - Einstieg: «Vom WebGIS holen» im Menue «Weitere Aktionen» von Haltungen UND Schaechten (vor
    GeoShop-Abgleich) sowie auf der Export-Seite; alle ueber EINEN `WebGisHolenAblauf`
    (UI/Services, eine Instanz im ServiceProvider). Anmeldung nur auf der Export-Seite.
  - Fenster `WebGisHolenWindow` NICHT modal (Wunsch Pascal): Doppelklick auf eine Zeile oeffnet Haltung/
    Schacht, «Neu pruefen», «Uebernehmen» (danach frisch geprueft); Spalten «Bisher» / «Aus dem WebGIS».
    Materialdetails in WebGIS-Schreibweise «Gruppe, Detail (Kuerzel)» werden erkannt (Beton, unbekannt
    -> Beton; Beton, Fertigteil -> Fertigbetonelement); PAA/SAA-Blatt entscheidet Typ AA der Haltung.
    Ohne Vokabularbegriff (Schleuderbeton, Beton vorgespannt, GUP/GFK Fertigteil) gilt seit 24.09.2026 (Entscheid
    Pascal «die Felder und Bezeichnungen gibt es in SewerStudio»): Steht der Text in der Materialdetail-Liste der
    Objektakte (`haltung.material`/`schacht.materialdetail`, auch je Gruppe), wird er uebernommen wie bei einer
    Handauswahl in der Akte (`ObjektaktenBearbeitung.Normalisiere`, meist woertlich); nur was auch dort fehlt, bleibt
    ein Hinweis. Normziel fehlt diesen Details weiter (DSS/XTF: «fachliche Zuordnung offen»).
    Gesperrtes klar gekennzeichnet (Wunsch Pascal 23.09.): Band orange, sobald etwas gesperrt oder nicht
    zugeordnet ist (`WebGisImportBericht.Kopf`), darunter eine immer offene ROTE Liste
    (`WebGisImportBericht.NichtZugeordnet`: erst gesperrte Objekte/Massnahmen, dann Werte ohne Zuordnung, je mit
    Grund). Keine zugeklappte Hinweisliste mehr. Tests `WebGisImportBerichtTests`, `WebGisHolenFensterUiTests`.
  - Bericht
    `__WebGIS_Export/WebGIS_Holen-Vorschau_*.txt`. Tests: `WebGisImportWertTests`,
    `WebGisImportPlanBuilderTests`, `WebGisImportUebernahmeTests`, `WebGisSanierungImportTests`,
    `WebGisImportUseCaseTests`, `WebGisImportBerichtTests`, Gruppenfaelle in `WebGisExportUseCaseTests`.
  - GLOBALID (Entscheid Pascal 23.09.2026): `HaltungRecord/SchachtRecord.WebGisGlobalId` wird nur
    bei genau einem Suchtreffer mit exakt gleichem Namen gespeichert (Holen). Ist sie gespeichert,
    liest JEDER Schritt (Plan, frisch vor dem Schreiben, Nachkontrolle, Massnahmen, Holen) nur ueber
    sie (`WebGisObjektLesen.LiesAsync` -> `LeseUeberGlobalIdAsync`), nie ueber den Namen — auch nicht
    als Rueckfall. Der Name kommt dann aus der Maske (`WebGisFeldkarte.BezeichnungRef`, refIds aus der
    Inventur v2, LIVE NOCH NICHT GEPRUEFT) und muss exakt dem Projektnamen entsprechen, sonst Sperre;
    fehlt er, ebenfalls Sperre. Anlass Zone 1.15: ab 14:32 fand die Namenssuche nichts mehr (182/182
    gesperrt). GeoShop-TID (`ch24gwkd…`) und OBJECTID sind KEINE GlobalID. Tests:
    `WebGisExportUseCaseTests` (Gespeicherte_globalid_…), `WebGisImportUseCaseTests`, `GeonisWebGisClientTests`.
  - KANALFIRMA = IST-ZUSTAND (Entscheid Pascal 23.09.2026 spaet, «ergaenzen und korrigieren»):
    (1) SENDEN: Ein Wert der Kanalfirma (Herkunft `FieldSourceRegeln.IstKanalfirma`: Legacy, Protocol, Xtf,
    Xtf405, Ili, Pdf, Spro; ohne Handmarke), der vom WebGIS abweicht, wird NIE automatisch geschrieben. Er
    steht als `WebGisVorschlag` an der Position (`Vorschlaege`) — nur Felder der `WebGisHandwertKarte`, nie
    eigene Regeln, Laenge, Eigentum, Betreiber, Baujahr, nie Kataster-Herkunft, nie wenn ein Handwert dasselbe
    Feld belegt. Fenster: gelber Bereich «Kanalfirma weicht vom WebGIS ab — n von m angehakt», je Zeile ein
    Haekchen, nichts vorangehakt, «Alle anhaken»/«Keine». `WebGisPlanVergleich` zaehlt angehakte Vorschlaege mit
    (`WebGisVorschlagAuswahl.Wirksam`: ein Haken mehr oder weniger ist ein anderer Plan). «Jetzt schreiben»
    baut ueber `WebGisExportUseCase.BaueFrischenPlanAsync(projekt, bestaetigt)` und uebertraegt dabei die Haken
    (`UebertrageAuf`, Schluessel Objekt+Feld+Wert — ein geaenderter Wert ist ein neuer, NICHT angehakter
    Vorschlag); erst `FuehreAusAsync` macht sie zu Aenderungen (`UebernimmGewaehlte`), also nach dem Vergleich.
    `WebGisExportPlan.NichtsZuSchreiben`, `WebGisUebersicht.NichtsZuTun` kennen sie. Neue WebGIS-Logik gehoert
    in den UseCase, nicht in die Export-Seite.
    Bericht: `[x| ] Kanalfirma weicht ab`. (2) IMPORT: Eine Kanalfirma-Lieferung ersetzt Kataster-Werte
    (GeoShop/QGIS/WebGIS) ohne Handmarke. `KatasterFeldschutz` sperrt nur noch einen Schreibversuch
    UNBEKANNTER Herkunft (Konfliktmarke bleibt); `MergeEngine`: Kataster = Prioritaet 10 (unterste).
    Handwerte bleiben immer. Folge: Ein im GeoShop-Feldvergleich behaltener GeoShop-Wert wird von einer
    spaeteren Kanalfirma-Lieferung ersetzt, wenn er nicht von Hand gesetzt ist. AUSNAHME LAGE:
    `Koordinate_East`/`Koordinate_North` aus dem Kataster ersetzt kein Import (vermessen gegen Handy-GPS;
    `KatasterFeldschutz.Pruefe(feld, …)`), nur ein neuerer Katasterstand oder die Hand; eine leere Lage
    fuellt das Protokoll. Waechter `SchachtProtocolVollstaendigkeitTests.Koordinaten_ueberschreiben_…`,
    `NachgeschlagenerWertMergeSchutzTests.Eine_vermessene_katasterlage_…`. (3) DOPPELTE GLOBALID:
    `WebGisEindeutigkeit.SperreDoppelte` im Export UND im Holen — zeigen zwei Datensaetze auf dasselbe
    WebGIS-Objekt, bekommt keines etwas (Holen: weder Werte noch GlobalID); `Uebernimm` vergibt eine GlobalID
    nie an einen zweiten Datensatz derselben Art. Tests `WebGisExportUseCaseTests.Kanalfirma`,
    `WebGisVorschauFensterUiTests`, `NachgeschlagenerWertMergeSchutzTests`, `GeoShopRobusterImportTests`,
    `WebGisImportUseCaseTests` (Zwei_datensaetze_…, Eine_globalid_…).
  - WEBGIS-BEGRIFFE, SCHRITT A (Entscheid Pascal 23.09.2026; Entwurf
    `docs/superpowers/specs/2026-09-23-webgis-begriffe-design.md`, Plan `docs/superpowers/plans/2026-09-23-webgis-begriffe-schritt-a.md`):
    Was ins WebGIS (Trigonet) geht, steht in SewerStudio als WebGIS-Beschriftung, zeichengenau («In Betrieb»,
    «Kreisprofil (K)», «Tot/Aufgehoben, verfüllt», «Regenabwasser»). Felder: Status, Lagebestimmung, Funktion
    hydraulisch, Verbindungsart, Bettung, Sanierungsbedarf, Nutzungsart, Profiltyp (Haltung) sowie Status,
    Lagebestimmung, Sanierungsbedarf, Nutzungsart und NUR beim Normschacht die Funktion. EINE Quelle:
    `WebGisBegriffe`/`WebGisWerteliste` (Domain) aus `Objektakten.Katalog.json`; Alt-/Importschreibweisen ueber
    Faltung, belegte Aliase (tot, Niederschlagsabwasser->Regenabwasser, Bachwasser->Bachabwasser,
    Pumpwerk->Pumpenschacht, Anderes (A)->Andere (A)) und das Vokabular als Vorstufe. Kein Treffer = Wert bleibt
    stehen, Projektpruefung meldet ihn, er geht nie ins WebGIS. Umgewandelt wird ZENTRAL beim Schreiben
    (`HaltungRecord.SetFieldValue`, `SchachtRecord.SetFieldValue`/`FuelleLeeresFeld`, Funktion nur Normschacht) —
    sonst verglichen Import und Katasterschutz Norm- gegen WebGIS-Begriff (Scheinkonflikte, Schlusspruefung
    23.09.). Dazu `ProjectVocabularyNormalizer` fuer alte Projektdateien (Laden/Speichern, FieldMeta unberuehrt),
    Holen woertlich (`WebGisImportWert`, Funktion nur beim Normschacht), Objektakte ohne Uebersetzung,
    QGIS/GeoShop ueber `QgisFeldKarte` und `GeoShopAttributZuordnung`; `GeoShopZiel.SchreibeVergleich` akzeptiert
    den gleichbedeutenden WebGIS-Begriff. Auswahllisten ueber `FieldCatalog.GetComboItems`
    (FieldDefinition.ComboItems dieser 8 Felder = null, niemand liest sie); ein Altwert wird zeichengenau
    HINTEN an die Liste gehaengt, sonst zeigt die Tabelle ihn leer und der erste Klick loescht ihn. Normseite:
    `SiaWerteliste.NachNorm` kennt Umlaute/Kuerzel/Aliase; 18 reine WebGIS-Werte ohne Norm stehen namentlich in
    `WebGisBegriffeNormTests`/`DropdownExportierbarkeitTests`; DSS bleibt fail-closed. Zustandsklasse bleibt
    Ziffer. «Unbekannt» ist gueltig, das Holen fuellt damit nichts. Masse (Entscheid A): Zahl bleibt, nur
    Zahlen der WebGIS-Liste sind sendbar, Projektpruefung meldet den Rest. Faltung schreibt ZUERST klein
    (sonst «Ueberschiebmuffen» nicht erkannt). Offen: Schritt B (Material), C (FunktionHierarchisch/Typ AA,
    vor Beginn neu klaeren), Funktionslisten fuer Spezialbauwerk/Versickerungsanlage nicht erhoben.
  - SCHUTZREGELN (Pruefung 23.09.2026, `WebGisSchreibschutzTests`): (1) `WebGisPlanVergleich` vergleicht
    ALT und NEU — eine fremde Aenderung im WebGIS seit der Vorschau erzwingt eine neue Vorschau statt stillem
    Ueberschreiben. (2) Senden: Felder mit WebGIS-Liste nur mit zeichengenauem WebGIS-Begriff, keine Faltung,
    keine Punkt-Regel; doppelter Maskeneintrag sperrt das Feld. (3) Holen: BEWUSST LEER = GESCHUETZT (Entscheid
    Pascal, wie GeoShop; `WebGisImportFeld.Handwert`, `BaujahrHandwert`); `Uebernimm` prueft je Feld nochmals
    Handmarke, Wert seit der Vorschau und bei der Schachtfunktion die AKTUELLE Bauwerksart. «Leere Felder aus
    QGIS» hatte bis 02.10.2026 eine eigene Regel (die Leere entscheidet); seit dem Entscheid E3 gilt ueberall
    «bewusst leer bleibt leer», gemeinsame Stelle `FuelleLeeresFeld` (`xtf-kataster-qgis.md`). (4)
    `HaltungRecord.FuelleLeeresFeld` speichert ebenfalls den WebGIS-Begriff.
  - BEWUSST LEER, EINE REGEL FUER ALLE FUELLWEGE (Entscheid Pascal 02.10.2026, E3): `HaltungRecord`/
    `SchachtRecord.FuelleLeeresFeld` fuellen ein bewusst leeres Feld (`IstBewusstLeer`: `UserEdited` und leer)
    nicht mehr. Beim Holen schuetzte schon der Planer und `SeitVorschauUnveraendert`; neu haelt auch die
    Uebernahme selbst stand (`WebGisImportUebernahmeTests.Bewusst_leeres_feld_wird_bei_der_uebernahme_nicht_gefuellt`).
    Die Ausnahme vom 24.09.2026 bleibt: Den Eigentuemer fuehrt das WebGIS, er ersetzt auch eine bewusst leere
    Handeingabe — `WebGisImportUseCase.FuelleLeer` nimmt dafuer die Handmarke vor dem Fuellen weg und setzt sie
    zurueck, wenn nichts geschrieben wurde (`Bewusst_leerer_eigentuemer_wird_bei_der_uebernahme_gefuellt`).
  - SCHREIBSCHUTZ, EINDEUTIGKEIT, AENDERUNGSDATUM (Entscheid Pascal 23.09.2026 abends): Eigentum,
    Betreiber, Haltungslaenge (alle Laengenfelder), Baujahr, GlobalID, Objekt-ID (OBJECTID) und die
    Bezeichnung werden im WebGIS nie ueberschrieben; das Baujahr darf nur ein LEERES Feld fuellen — bei
    Haltung UND Schacht («alles gilt auch bei den Schaechten», Schacht-Baujahr `e35e99dc…`). `WebGisGeschuetzteFelder` (refIds aus Inventur v2) ist die zweite Sperre neben der Feldliste:
    genannte Schutzfelder UND alles, was der Export nicht planen kann (Freigabeliste = Zustand,
    Sanierungsbedarf, Bemerkung, leeres Haltungs-Baujahr, `WebGisHandwertKarte`), sperren das ganze
    Objekt — geprueft in `WebGisExportUseCase.SchreibeEineAsync` UND in `GeonisWebGisClient.SchreibeAsync`
    gegen den eben gelesenen Stand. Zeigen zwei SewerStudio-Objekte auf dieselbe GlobalID (gefunden oder
    gespeichert), werden beide gesperrt und bekommen keine Massnahme (`SperreDoppelteZuordnungen`, erst nach
    dem Lesen ALLER Objekte). `WebGisExportPosition.GelesenerStand` haelt ALLE Maskenfelder beim Planen
    (samt «Geändert am (UTC)», dessen refId nicht erhoben ist); weicht der frische Stand vor dem Schreiben in
    irgendeinem Feld ab, wird nicht geschrieben («seit der Prüfung geändert»), und `WebGisPlanVergleich`
    nimmt den Fingerabdruck mit (neue Vorschau statt Schreiben). Live noch nicht belegt, dass zwei Lesungen
    ohne Bearbeitung identisch sind — sperrt der erste Probelauf ALLE Objekte mit derselben Feldkennung,
    ist das ein fluechtiges Feld, nicht eine fremde Aenderung. Tests `WebGisGeschuetzteFelderTests`,
    `WebGisExportUseCaseTests.Identitaet`, `GeonisWebGisClientTests.SchreibeAsync_*`.
  - Geplant (Entscheid Pascal 23.09.): statt zehn Abgleichknoepfen «Vom Kataster holen» (WebGIS,
    sonst GeoShop, sonst QGIS; Kennungen dabei) und «An den Kataster senden» (WebGIS, XTF unter
    Weitere). Weitere Maskenfelder erst nach Beschriftungs-Inventur und Pruefung am echten Objekt.
  - WEBGIS-TEIL DER EXPORT-SEITE (24.09.2026): `ExportWebGisBereich` (ViewModels/Pages) traegt Anmelden, Pruefen/
    Schreiben und Holen; die Export-Seite haelt ihn als `WebGis` und reicht nur die genutzten Dienste herein
    (`ExportWebGisBereich.Dienste`), nie den ServiceProvider. Die Oberflaeche bindet `WebGis.Status` und
    `WebGis.*Command`; `ExportWebGisBereichTests` prueft jeden Pfad (WPF meldet einen falschen Pfad nicht). Damit sind
    `ExportPageViewModelDependencyTests`, `ArchitectureDriftRatchet` und der Groessenwaechter wieder gruen. Die
    Schachtseite blieb unter 2000 Zeilen, weil ihr Importschutz (`SharedProtocolImportOperationState`,
    `ProtocolImportShellOperationGuard`) unveraendert in `SchaechteProtokollImportSperre.cs` liegt.
    Damals rot und NICHT daher: `SchaechteNovaLayoutIsolatedSmokeTests.Schachtansicht_laedt_Lage_in_der_Liste…`
    haengt ueber 60 s, auch auf dem Stand vor dem Umbau (916fd626c, 24.09. gemessen, SewerStudio lief).
    Ursache ist das offene Programm, kein Codefehler: Am 01.10.2026 hingen dieser Test, sein Kindprozess
    und `TaskbarFortschrittThreadSafetyIsolatedSmokeTests` bei offenem SewerStudio auch einzeln; nach dem
    Schliessen liefen alle drei in 13 s gruen. Vor einem Push SewerStudio schliessen.
  - HOLEN: TYP AA UND DIE GANZE SCHACHTMASKE (Wunsch Pascal 24.09.2026 «importiere das was im WebGIS ist», «auch
    bei den Schächten alle fehlenden Felder ergänzen»). `WebGisImportAktenfelder`: (1) Typ AA (PAA/SAA) kommt aus dem
    WebGIS selbst (`WebGisFeldkarte.TypAaRef`, Haltung `65e83cb4…`, Schacht `8d17a0bc…`, Inventur v2) und entscheidet
    «Liegenschaftsentwässerung» u. a., die es unter PAA und SAA gibt — vorher 22 Schächte in Zone 1.15 «passt zu
    mehreren»; sonst Rueckfall auf Typ AA/Praefix in SewerStudio. Typ AA geht in `haltung.aatype`/`schacht.typ_aa`,
    aber nie gegen die Funktion der Haltung (Kanalfirma «PAA.…» bleibt -> Hinweis). (2) Am Schacht fuellt das Holen
    rund 30 Felder der SCHACHTAKTE nach der WebGIS-Maske (Funktion hier./hydr., Lage-/Hoehenbestimmung und -genauigkeit,
    Ebene, Zugaenglichkeit, Rotation, Gelaende-/Sohlen-/Deckelhoehe, Rueckstaukote, Intervention, Amphibienausstieg,
    Informationsquelle, Steuerung, Buero, Standortgemeinde, Finanzierung, Wiederbeschaffungswert/-Basisjahr/-Bauart,
    Erhebungsjahr, Intervalle, Systemgrenze, Hfrei, Sachbearbeiter, Akten). Auswahl ueber den WebGIS-Schluessel =
    Originalcode der Aktenliste; eine refId aus der Inventur zaehlt nur, wenn die gelesene Liste zur Aktenliste passt
    (>= 2 Eintraege gleicher Schluessel UND Text, kein Widerspruch; Organisationslisten: Schluessel «Bund»); ohne refId
    (Funktion hydr., Lage-/Hoehengenauigkeit) wird die EINE passende Liste der Maske gesucht. Zahlen «0.###», Jahre
    1800-2200, sonst Hinweis. Leer fuellen, Wert ohne Handmarke (GeoShop) ersetzen, Handwert (auch bewusst leer) nie.
    (3) Funktion hier., Sohlen-/Gelaendehoehe, Rotation, Ebene, Lagebestimmung des Schachts schrieb das Holen bisher in
    Tabellenfelder, die die Schachttabelle nicht fuehrt (nur «Bisherige Angaben» der Akte) — die Hoehenrechnung liest
    aber `schacht.sohlenhoehe` usw. Seither nur noch in die Akte (`SchachtNurUeberAkte`). Nicht geholt: Zustand,
    Sanierungsbedarf, Bemerkung (SewerStudio ist Quelle), berechnete/Systemfelder, OBJECTID, die Paare «Bezeichnung
    alter./hist.» und «Rechtswert/Hochwert» (je nur eine refId bekannt). Tests `WebGisImportTypAaTests`.
- WG05 (28.09.2026, Entscheid Pascal): **Massnahmen nur an nachgeprüften Elternobjekten und nach dem Anlegen
  gegengeprüft.** Hat GEONIS ein geplantes Feld des Elternobjekts nicht übernommen oder liess es sich nicht
  zurücklesen (`WebGisExportPosition.Nachgeprueft` false), wird keine Massnahme angelegt (ersetzt den Audit-Test
  «bekommt trotzdem die Massnahme»). `newId` ist die OBJECTID, nicht die GlobalID (live 28.09.): Nach bestätigtem
  Anlegen liest `PruefeMassnahmeNachAsync` die Liste am Elternobjekt neu, verlangt GENAU EINE neue GlobalID, liest
  sie per `LeseMassnahmeAsync` und vergleicht jedes geplante Feld (Sanierungsjahr nur als Jahr). Nur dann
  `Nachgeprueft`; sonst `Ungeklaert` (Grund) — nie «nicht angelegt», nie automatisch wiederholt, weitere Massnahmen
  des Laufs werden nicht mehr angelegt, `WebGisSendenAblauf.Ausgang.Ungeklaert`. Bericht/Log/Liste unterscheiden
  bestätigt / nachgeprüft / ungeklärt. Tests `WebGisExportUseCaseTests.Audit.cs` (WG05-Block).
- WG06 (28.09.2026): **Holen und Lesefehler.** `GeonisWebGisClient.PruefeStatus`: HTTP 401/403 bei Maske
  (`getLayoutDataCombined`, auch `LeseMassnahmeAsync`), leerer Sanierungsmaske (`getEmptyData`) und Auswahllisten
  (`getControlValues`) ist eine `WebGisSitzungException` wie bei der Suche; jeder andere Fehlstatus (5xx …) und eine
  Maske ohne [Layout, Daten] bzw. kein JSON-Objekt ist eine `WebGisAntwortException` — nie mehr «nicht gefunden».
  Vor dem Anlegen einer geholten Sanierungsakte prüft `PruefeVorUebernahmeAsync`, dass ihre GlobalID noch in der
  frisch gelesenen Liste DESSELBEN Elternobjekts steht (umgehängt/gelöscht = Sperre mit Grund). Entscheid Pascal:
  Unbekannter Status oder unbekanntes Verfahren (kein Treffer in der SewerStudio-Liste, auch ein Schlüssel ohne
  Eintrag in der WebGIS-Liste) sperrt die GANZE Akte, keine Teilübernahme; übrige Auswahlfelder bleiben Hinweis.
  Tests `GeonisWebGisClientRobustheitTests` (WG06-Block), `WebGisImportUseCaseTests`, `WebGisSanierungImportTests`.
- OFFEN / NICHT ERLEDIGT: Abstimmung mit Trigonet (interne Schnittstelle, ein Schreibweg).
  Der reale Schreibweg ist bisher nur manuell im Browser und im Lauf vom 21.09. belegt
  (Haltung 525145-505377: Z4 + Sanierungsbedarf Saniert + Bemerkung; zwei Sanierungsmassnahmen
  s.o.). Naechster Schritt: Probelauf an Bürglen ueber die Export-Seite — erwartet 0 Klartext-
  Konfliktsperren und «Beton, Fertigteil» als Aenderung statt Hinweis.
- Datenlage Bürglen vor Voll-Export bereinigen (siehe
  `__WebGIS_Export/Probelauf-Vorschau_20260921.txt`): 9 Haltungen ohne Sanierungs-Akte,
  Schacht 525145/60122, 7 Akten ohne Verfahren, 6 Laengen (WebGIS gewinnt).

