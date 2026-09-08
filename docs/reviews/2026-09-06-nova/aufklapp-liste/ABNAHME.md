# Abnahmepr?fung: Nova-Aufklapplisten und Grafiken, 08.09.2026

**Ergebnis: technische Nachpr?fung mit Korrekturen; keine uneingeschr?nkte Gesamtfreigabe des Redesigns.**
Die beiden Listen, ihre Grafiken und der Wechsel zur Tabelle funktionieren in den gepr?ften Abl?ufen.
Die unten ausdr?cklich aufgef?hrten Abweichungen bleiben offen. Merge und Push sind noch nicht erfolgt.

Gepr?ft im getrennten Arbeitsordner `C:\Sewer-Studio_KI_4.5-nova`, Branch
`feature/nova-haltungsliste`, Ausgangsstand `1cb7cb7c361cffce332971bccc8473878acf1c8e`.
Der Hauptordner steht auf `feature/eval-pruefsatz-review` und enth?lt zus?tzlich ungesicherte
XTF- und Projektwechsel-Arbeit. Diese Pr?fung hat dort keine Dateien ver?ndert.

## Anlass und Umfang

Pascal w?nscht eine ?bersichtsliste mit aufklappbaren Haltungen und Sch?chten; die Grafiken
sollen dem AWU-Haltungsprotokoll beziehungsweise dem WinCan-Schachtschnitt entsprechen.
Grundlagen: [Plan](../../../superpowers/plans/2026-09-08-nova-haltungen-aufklapp-liste.md),
[?bergabe](UEBERGABE-CODEX.md) und die dauerhaft gesicherten
[Entscheidungen und Restpunkte](ENTSCHEIDUNGEN-UND-RESTPUNKTE.md).

Diese Pr?fung betrifft den ?bergebenen Branch und die abschliessende Fixrunde. Sie ersetzt
nicht den separaten Gesamtbericht vom Morgen und erkl?rt dessen offenen Punkte nicht
pauschal f?r behoben. Insbesondere die Arbeiten an Suche und Projektwechsel liegen getrennt
im ungesicherten Hauptordner.

## Pr?fung der Aufgaben 1?7

| Aufgabe | Ergebnis | Nachweis und Grenze |
|---|---|---|
| 1: Haltungs-Control und Formular | Bestanden im gepr?ften Umfang | Akkordeon, Auswahl, Pfeil, Themen 17/9/11/3 sowie Weitere Angaben; bestehende Fokus-, Formular- und Virtualisierungstests; [hell offen](bilder/haltungen-liste-auf-hell.png) |
| 2: Liste als Standard, Tabelle erreichbar | Bestanden | Echter Men?wechsel Liste ? Tabelle ? Liste mit echtem ViewModel; genau ein aktiver Formular-Abgleich; Projektdaten vor/nach bytegleich serialisiert; vier `ansichtswechsel-*.json` unter nachweise |
| 3: Bilder und Dokumentation | Bestanden | Acht aktuelle Bilder einzeln angesehen; Messungen, Pr?fhost und Entscheidungen gesichert |
| 4: Haltungsgrafik | Bestanden mit dokumentierten Grenzen | Rohrs?ule, Skala, Knoten und drei Befundsymbole; [dunkel offen](bilder/haltungen-liste-auf-dunkel.png); Aktualisierung nach Wiederladen zus?tzlich repariert und getestet |
| 5: Schachtgrafik | Bestanden mit Darstellungsgrenzen | Konus/Wand/Sohle, Tiefe, Innenmasse und Symbole; [hell](bilder/schaechte-liste-zu-hell.png), [dunkel](bilder/schaechte-liste-zu-dunkel.png); Anschlussname am Rand gek?rzt, grosser Fliesspfeil bleiben |
| 6: Schachtliste | Funktion bestanden, Themenzuordnung abweichend | Standard Liste, Umschalten, genau ein Formular nach Fix `ed3268616` best?tigt; [hell offen](bilder/schaechte-liste-auf-hell.png), [dunkel offen](bilder/schaechte-liste-auf-dunkel.png); die versprochenen Themenzahlen 10/9/7/4 sind im echten Pr?fprojekt nicht erreicht |
| 7: Schlusspr?fung und Grafiken-Nachtrag | Pr?fung und Doku vorhanden | Endg?ltige Testzahlen unten; ?bernahme in Hauptordner und Push bleiben ausstehend |

Weitere Bilder: [Haltungen geschlossen hell](bilder/haltungen-liste-zu-hell.png),
[Haltungen geschlossen dunkel](bilder/haltungen-liste-zu-dunkel.png).

## In dieser Nachpr?fung korrigiert

1. Beide Grafik-Controls meldeten sich beim Entladen vom Datensatz ab, beim Wiederladen aber
   nicht wieder an. Sp?tere Protokoll?nderungen erschienen deshalb nicht mehr. Jetzt werden
   die Meldungen beim Wiederladen erneut verbunden, bei Haltungen auch die Eintragsliste.
   `NovaGrafikWiederladenTests`: tats?chliches Entfernen/Wiedereinsetzen im Fenster,
   danach Protokoll?nderung; automatische neue Symbolzahl ohne manuellen Zeichenaufruf.
   Beide F?lle scheiterten vor der Korrektur und bestehen danach.
2. Beide Listencontroller verbanden beim erneuten `Verdrahte` zwar ihr Aufklapp-Ereignis,
   aber nicht das schon offene Formular. Sie stellen jetzt den Formular-Abgleich wieder her.
   `NovaListenWiederverbindenTests` pr?ft eine externe Feld?nderung nach Ab-/Wiederanmeldung.
3. Der ?bergebene Test zum Entsorgen des Schachtformulars hatte kein `FieldName` am Testfeld.
   Dadurch war das Feld gar nicht mit dem Live-Abgleich verbunden. Der Test besitzt jetzt
   die Feldkennung und pr?ft zuerst die echte Aktualisierung, danach deren Ende und den R?ckweg.
   Auch der erste eigene Listen-Test ben?tigte diese Korrektur seiner Testdaten.
4. Gek?rztes PROTOK? durch PDF mit Hinweis Protokoll ersetzt. INNENMASS 1/2 wurde zu
   INNEN 1/2 mit vollst?ndigem Hinweis inklusive mm. Die Pr?fmarke verwendet die vorhandene
   kontrastreichere `SuccessTextBrush`. Der bestehende Kopftext-W?chter verlangt weiterhin
   alle Spalten und Grossschreibung; nur der erwartete Titel wurde angepasst.
5. Der Pr?fhost unterst?tzt jetzt `schaechteliste`, erstellt zu/auf-Bilder und pr?ft bei
   beiden Seiten anschliessend den tats?chlichen Men?wechsel. Fehler im Fotoablauf f?hren
   zu einem Fehlerstatus. Neue Pr?flogik steht in einer kleinen separaten Teildatei.

## Offene Abweichungen und Grenzen

- **Schacht-Themenzuordnung weiterhin falsch:** `SchaechteColumnPolicy.ResolveSchachtDetailGroup`
  sortiert unter anderem Baujahr, Inspektionsdatum und Fotos unter Weitere Angaben;
  Eigent?mer steht bei Stammdaten. Das ist derselbe Grundbefund wie R7 im Morgenaudit.
  Im Pr?fprojekt mit der ausgelieferten Vorlage erscheinen 10/1/2/2 plus 17 Weitere Angaben,
  nicht 10/9/7/4. Die Zahlen sind projekt-/vorlagenabh?ngig; die konkreten falsch zugeordneten
  Namen belegen die Abweichung. Der Dokumente-Bereich bleibt im offenen Foto unter seinem
  Z?hler optisch leer. Seine vollst?ndige Bedienbarkeit ist damit nicht abgenommen.
- Die Zustandsmarke bleibt in Listenzeile und ?bersichtsbereich doppelt sichtbar. Das ist
  eine ausdr?cklich ?bernommene Darstellungsgrenze, keine zweite Zustandsberechnung.
- Der Anschlussname in der Schachtgrafik bleibt am Rand gek?rzt; der Fliesspfeil ist gross.
  Das hervorgehobene Feld Sanieren Ja/Nein hat im dunklen Thema weiterhin einen sehr hellen
  Hintergrund mit schwacher Beschriftung. Die behobene Pr?fmarke ist davon unabh?ngig.
- Fr?here Restpunkte bleiben dokumentiert: kein Leerzustand der leeren Liste, Abdocken nur
  in der Tabelle und ohne neue vollst?ndige Bedienabnahme, fehlender Konflikthinweis auf der
  Schachtseite als Bestandsl?cke, eingeschr?nkte Textkategorien der Schachtgrafik,
  feste Grafik-H?he statt dynamischem 55-Prozent-Anteil und kein PDF-Hashvergleich.
  Vollst?ndiger Verlauf mit Entscheidungen: ENTSCHEIDUNGEN-UND-RESTPUNKTE.md.
- Die ?lteren ?bersichts-Panels besitzen weiterhin eigene Abonnements; eine l?ckenlose
  Speicherleckpr?fung s?mtlicher Seitenzyklen wurde nicht durchgef?hrt. Die neu gepr?ften
  Wiederladef?lle betreffen ausdr?cklich Grafiken und Listenformulare.
- Gemessen wurde 1920 ? 1080 bei 96 DPI / 100 Prozent. Windows-Skalierung 125/150 Prozent,
  Screenreader und s?mtliche manuellen Arbeitsabl?ufe im echten Projekt sind nicht gepr?ft.
  Der Pr?fhost erfasst die vom Windows-Compositor gezeichnete Mica-Fl?che nicht; f?r die
  Fotos f?llt er sie mit der Theme-Fl?che aus. Ein Foto ist kein Beleg f?r echte Mica-Darstellung.
- Kein produktiver Start, keine Kundenoriginale ge?ndert, keine laufende App beendet.
  Die Foto-Projekte sind k?nstlich (40 Haltungen, 8 Sch?chte), Profil/Wissen/QGIS-Pfade liegen
  unter `.tmp/nova-abnahme-codex/bedienung`. `Application.OnStartup` ist unterdr?ckt.

## Messungen und Testweg

Geschlossene Haltungsliste: 22 zumindest teilweise sichtbare Kopfzeilen, davon im Bild
mindestens 21 vollst?ndig; 23 erzeugte Zeilen bei 40 Datens?tzen und kein Formular.
Geschlossene Schachtliste: alle acht Datens?tze, kein Formular. Aufgeklappt jeweils vier
`RecordDetailsView`-Instanzen (Weitere Angaben ist geschlossen), genau ein offener Datensatz.
Die lange Eingabe wird vertikal gescrollt; nicht alle Felder passen gleichzeitig auf den Bildschirm.
Die Messungen sind unter [nachweise](nachweise) abgelegt.

- Erste gezielte Nachpr?fung: 274 bestanden, 7 ?bersprungen, 0 Fehler.
- Vier Fotoabl?ufe (Haltungen/Sch?chte ? hell/dunkel): alle Exit 0; Men?-R?ckwege und
  unver?nderte Projektdaten in den vier `ansichtswechsel-*.json` best?tigt.
- Vollst?ndiger finaler Release-Build: 0 Warnungen, 0 Fehler; [Buildprotokoll](nachweise/release-build-final.txt).
- Der erste volle Testlauf enthielt neben dem bekannten Nachschlag-Test noch zwei Fehler
  in den eigenen unvollst?ndigen Testdaten und einen veralteten erwarteten Spaltentitel.
  Diese wurden korrigiert; der Oberfl?chenlauf wurde vollst?ndig wiederholt. Der urspr?ngliche
  Lauf wird unter [tests-erster-gesamtlauf.json](nachweise/tests-erster-gesamtlauf.json) offengelegt.
- ?bersprungene Kindprozess-Einstiege bedeuten nicht, dass ihr Szenario ungepr?ft blieb:
  Die zugeh?rigen Elternpr?fungen starten einen eigenen Prozess und verlangen dessen
  Szenariobest?tigung. Echte externe Integrations-Skips bleiben von dieser Aussage ausgenommen.

| Testprojekt | Bestanden | Fehler | ?bersprungen |
|---|---:|---:|---:|
| ProjectModernizer.Tests | 62 | 0 | 0 |
| AuswertungPro.Next.Pipeline.Tests | 2644 | 0 | 3 |
| AuswertungPro.Next.Infrastructure.Tests | 6291 | 0 | 6 |
| AuswertungPro.Next.UI.Tests | 6864 | 1 | 17 |
| **Summe** | **15861** | **1** | **26** |

Einziger roter Test: `NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern`.
Der schon vor diesem Branch bekannte Kindprozess scheitert am Zeitlimit; gem?ss ?bergabe
kein Regress dieser Arbeit. Der vollst?ndige Lauf ist deshalb formal weiterhin rot.
Es wurden keine Tests deaktiviert. Neue Wiederladef?lle und der verst?rkte Schachttest bestehen.
Maschinenlesbare Zahlen: [tests-final.json](nachweise/tests-final.json);
[abschliessendes UI-Protokoll](nachweise/ui-final.txt). Die drei ?brigen Testprojekte stammen
vom vollst?ndigen Gesamtlauf; nach den letzten Anpassungen der UI-Testdaten und des
Kopftext-W?chters wurde der komplette UI-Lauf wiederholt.


## Schlusspr?fung und ?bernahme

Schwerpunkte der Codepr?fung: schreibfreie Ansichts-Builder, vorhandene R?ckschreibwege,
Handwert-Stempel, Liste/Tabelle-Auswahl bei L?schen und Kontextmen?s, Ab-/Wiederanmeldung,
SVG-Vertrag und belastbare Testaussagen. Die behobenen Wiederladefehler und der verst?rkte
Schachttest stammen aus dieser Pr?fung. Kein neues NuGet-Paket, keine Datenformat?nderung,
keine Erweiterung der fast ausgesch?pften Seitenklassen. Dies ist keine Garantie, dass jede
m?gliche Kombination aller Funktionen fehlerfrei ist.

**?bernahme ausstehend:** Die ?bergabe verlangt vor dem Merge ein geschlossenes SewerStudio;
AGENTS.md verbietet automatisches Beenden. Bei der Pr?fung lief SewerStudio aus dem Hauptordner
(PID 46284). Deshalb bisher kein Stash, kein Merge und kein Push. Vor einer sp?teren ?bernahme
muss der aktuelle ungesicherte Hauptstand erneut vollst?ndig inventarisiert und gesichert werden;
er umfasst inzwischen auch die Projektwechsel-Korrekturen und die Auditberichte, nicht nur XTF.
