# Optik und Bedienung professionell — Kurzabnahme

Stand: 29.09.2026 · Branch `feature/optik-professionell` · Plan
`docs/superpowers/plans/2026-09-28-optik-professionell.md` (17 Aufgaben) · dieser Bericht
gehört zu Aufgabe 17 («Kleinigkeiten und Endkontrolle»), am Ende der Reihe.

Auftrag Pascal: «Setze alles um.» Ziel war ein einheitliches, professionelles Aussehen und
einheitliche Bedienwege im ganzen Programm — ohne Fachlogik, Datenformate oder Feldnamen
zu ändern. Dieser Bericht fasst zusammen, was sich geändert hat, was Pascal von Auge prüfen
sollte, und wo die Grenzen dieser Abnahme liegen. Einzelheiten mit allen Testnamen stehen in
`CLAUDE.md` unter der Überschrift «Optik und Bedienung professionell (28.09.2026)».

## Was sich geändert hat, nach Bereich

**Meldungen und Fenster (Aufgaben 1–4).** Alle Hinweis- und Rückfragefenster sehen jetzt
gleich aus wie das Programm selbst (vorher graue Windows-Kästchen, die im Dunkelmodus falsch
aussahen). Erfolgsmeldungen wie «gespeichert» oder «exportiert» erscheinen als kurze
Einblendung unten rechts (Toast) statt als Klick-weg-Fenster. Jedes Fenster hat jetzt densel­
ben Aufbau: Titel oben, Knöpfe unten rechts, der Hauptknopf ganz rechts, «Abbrechen» direkt
daneben.

**Programmidentität (Aufgabe 5–6).** Das Fenstersymbol ist jetzt das SewerStudio-Symbol
(vorher das Kundenwappen). Neues Fenster «Über SewerStudio» mit Version, Ordnern, Systeminfo.
Neues Menü «Hilfe» mit Handbuch (F1) und Tastenkürzeln (Strg+F1).

**Aufräumen der Menüs (Aufgaben 7–9).** «Weitere Aktionen» ist bei Haltungen und Schächten
jetzt gleich gegliedert (fünf Gruppen). Doppelte oder alte Menüpunkte sind weg. Die alten
Tabellenansichten sind aus den Menüs verschwunden und stehen jetzt gesammelt in den
Einstellungen unter «Frühere Ansichten». Export- und Importseite sind neu in klare
Abschnitte geteilt.

**Sprache (Aufgabe 10).** Englische Fachbegriffe im Training Center sind jetzt Deutsch.
Rund 1500 Textstellen im ganzen Programm haben jetzt richtige Umlaute statt «ae/oe/ue», und
kein «ß» mehr (Schweizer Schreibweise). Fehlermeldungen sind verständlicher statt englischer
Rohtext.

**Leere Listen und Ladebalken (Aufgabe 11).** Eine leere Liste zeigt jetzt überall dieselbe
freundliche Karte mit Symbol und Erklärung statt eines leeren weissen Felds. Ladebalken gibt
es nur noch in zwei Höhen.

**Feste Farben und Schriften (Aufgabe 12).** Handverlesene Farben und die Schrift
«Consolas» in einzelnen Fenstern sind durch die programmweiten Farbeinstellungen ersetzt —
diese Fenster passen sich jetzt korrekt an, wenn das Design gewechselt wird.

**Windows-Integration (Aufgabe 13).** Neue Design-Option «Wie Windows» (folgt automatisch
hell/dunkel). Unterstützung für Windows-Hochkontrastmodus. Fortschrittsbalken in der
Taskleiste bei Sicherung und Import.

**Suche (Aufgabe 14).** Strg+K findet jetzt auch Befehle («neues Projekt», «Einstellungen» …),
nicht nur Haltungen und Schächte.

**Berichts-Logo (Aufgabe 15).** Ein eigenes Logo (Einstellungen ▸ Berichte) wirkt jetzt in
allen PDF-/Excel-Berichten aus einer einzigen Quelle — vorher stand der Pfad an sieben
Stellen im Code.

**Rückgängig (Aufgabe 16).** Wird in einem eigenen Arbeitsbaum umgesetzt und **separat
zusammengeführt** — nicht Teil dieses Berichts. Bitte nach dem Zusammenführen extra prüfen.

**Aufgabe 17 (dieser Bericht) — Kleinigkeiten:**
- Übersicht: Zustandsliste zeigt jetzt Z0 (dringend) zuerst statt Z4; «Häufigste Schäden»
  nennt Code und Klartext nur noch einmal statt doppelt; leere Karten («Sanierungsverfahren»,
  «Häufigste Schäden» ohne Befunde) zeigen die einheitliche Leerkarte statt eines nackten
  Satzes.
- Haltungen/Schächte: Die technische Meldung «Spalten geladen: 30» ist weg (war nur für
  Entwickler interessant). Das rote Band «Lernbasis: 0 Fälle» über der Werkzeugleiste
  erscheint erst, sobald tatsächlich ein Fall gelernt wurde — vorher stand es dauerhaft rot
  über jedem frischen Projekt, ohne dass etwas zu tun gewesen wäre.
- Der Projektpfad im Tooltip unter dem Programmnamen zeigte bei einem gerade neu angelegten,
  noch nicht gespeicherten Projekt möglicherweise noch den Pfad des vorher offenen Projekts —
  behoben.
- Player: Kopf zeigt nur noch den Dateinamen, der volle Pfad steht im Tooltip. «Play»/«Stop»
  heissen jetzt «Abspielen»/«Stopp». Der unbeschriftete «···»-Knopf für feste
  Geschwindigkeitsstufen hat jetzt einen Namen für Screenreader. In der Bedienleiste ist nur
  noch der Codier-Modus-Knopf farbig hervorgehoben, «Abspielen» ist ein normaler Knopf.
- Training Studio: Die fünf Schadensstufen-Knöpfe (1–5) teilen sich jetzt immer die ganze
  verfügbare Breite und werden nicht mehr abgeschnitten. Die schmale Vorschlagstabelle links
  hat etwas engere Spalten, damit mehr davon ohne Bildlauf sichtbar ist.
- Ein Kontrastfehler in drei Trainingsstudio-Abzeichen behoben (grüner Hintergrund mit
  dunkler statt heller Schrift — vorher zu wenig Kontrast im Dunkelmodus).
- Vier kleinere Code-Stellen räumen den Standard-Logopfad jetzt über dieselbe Quelle auf wie
  Aufgabe 15 (keine sichtbare Änderung, nur eine Quelle weniger).
- Fenstertitel «Über SewerStudio» hiess doppelt «SewerStudio — Über SewerStudio», jetzt
  «SewerStudio — Über das Programm».
- Zwei kleine Rechtschreib-/Zeichensetzungskorrekturen («Schacht/Schächte» mit Umlaut, ein
  Satzzeichenfehler im GeoShop-Abgleichbericht).

## Was Pascal von Auge prüfen sollte

- **Windows-Skalierung 125 % und 150 %.** Alle automatisierten Prüfungen laufen bei 100 %
  (Full HD). Bei höherer Skalierung bitte kurz durch die Hauptseiten klicken.
- **Dunkelmodus und «Wie Windows».** In den Einstellungen alle drei Design-Optionen
  durchklicken; besonders die neuen/geänderten Stellen dieser Aufgabe (Übersicht,
  Trainingsstudio-Abzeichen, Player-Kopf).
- **Windows-Hochkontrastmodus**, falls genutzt (Systemsteuerung ▸ Erleichterte Bedienung).
- **Ein echtes, grosses Projekt öffnen** (nicht nur das künstliche Testprojekt dieses
  Berichts) — besonders die Übersichtsseite mit vielen Haltungen/Schächten und die
  Lernbasis-Anzeige nach etwas Kostenerfahrung.
- **Rückgängig (Aufgabe 16)** nach dessen Zusammenführung separat prüfen — nicht Teil dieser
  Abnahme.
- Player: kurz «Abspielen»/«Pause»/«Stopp» klicken und den «···»-Knopf (Geschwindigkeits­
  stufen) mit der Maus antippen — Tooltip sollte jetzt Sinn ergeben.

## Bildschirmfotos

Erzeugt mit dem isolierten Prüfhost `docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/`
(eigenes Profil unter `.tmp`, künstliches Testprojekt, kein Echtzeitspiegel, keine
QGIS-Brücke, kein KI-Start — **nie** das echte Benutzerprofil oder ein echtes Projekt).
Der Prüfhost war seit dem Ordnerumzug auf 5.0 (13.09.2026) kaputt (drei Pfade zeigten noch
auf den nicht mehr vorhandenen alten Ordner `C:\Sewer-Studio_KI_4.5-nova`) und wurde dafür
repariert.

In `docs/reviews/2026-09-28-optik/bilder/` liegen, je hell und dunkel:

- `uebersicht-hell.png` / `uebersicht-dunkel.png`
- `import-hell.png` / `import-dunkel.png`
- `einstellungen-hell.png` / `einstellungen-dunkel.png`

**Haltungen, Schächte, Player und Training Studio fehlen als Bildschirmfoto.** Der
Prüfhost wirft dort beim Seitenaufbau `XamlParseException: Ressource "ToolbarButton" nicht
gefunden` — ein Fehler NUR im Prüfhost, nicht im Programm selbst (die betroffene Funktion,
die persönliche Erledigt-Markierung, läuft seit dem 13.09.2026 produktiv und ist durch
hunderte automatisierte Tests abgedeckt, darunter viele, die genau diese Seiten in einem
echten, isolierten WPF-Fenster aufbauen). Die vermutete Ursache: Der Prüfhost baut die
Programmoberfläche aus dem XAML-Text neu zusammen, statt die fertig kompilierte Programmdatei
zu laden — dabei geht eine Verbindung zwischen zwei Formatvorlagen-Dateien verloren, die im
echten Programm funktioniert. Das selbst zu reparieren hätte einen grösseren Umbau des
Prüf­werkzeugs gebraucht, was nicht mehr zu dieser «Kleinigkeiten»-Aufgabe gehört. Bitte diese
vier Seiten stattdessen kurz im laufenden Programm ansehen (siehe oben).

## Ehrliche Grenzen dieser Abnahme

- Kein produktiver Programmstart für die Bildschirmfotos (eigenes Profil, künstliches
  Projekt) — das ist Absicht, damit nichts an echten Projektdaten verändert wird.
- Nur 3 von 7 geplanten Seiten liessen sich fotografieren (siehe oben); für die übrigen vier
  ist die automatisierte Testabdeckung vorhanden, aber kein Bild.
- Windows-Skalierung 125 %/150 % ist nicht automatisiert geprüft.
- Ein per Einstellung ausgetauschtes Berichts-Logo wirkt weiterhin **nicht** im
  Codiermodus-PDF-Export (dort bleibt nur das mitgelieferte Standardlogo) — das ist ein
  bereits in Aufgabe 15 dokumentierter, bewusster Rest, kein neuer Befund dieser Aufgabe.
- Die geschützten WebGIS-Fenster (Export/Holen) sind wie im Plan vorgesehen unverändert
  geblieben.

## Tests und Build

- `dotnet build AuswertungPro.sln -c Debug`: 0 Fehler, 0 Warnungen.
- `dotnet test tests/AuswertungPro.Next.UI.Tests` (alle `DesignAudit*`-Tests): 303/303 grün.
- `dotnet test tests/AuswertungPro.Next.UI.Tests` (ganzes Projekt): 7713 grün, 44 übersprungen
  (bekannte, gewollte Kindprozess-Selbstübersprünge), 0 Fehler.
- `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests`: grün (Details im
  Aufgabenbericht `task-17-report.md`).
