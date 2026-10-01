# Vollständige XTF aus GeoShop-Objektakten

Stand: 12.09.2026. Der Exportumfang ist begrenzt; der vollständige WebGIS-Nachbau ist noch nicht abgenommen.

## Bedienung

1. GeoShop-Abgleich ausführen und die gewünschten Angaben übernehmen. Eine Eigentümer-JSON nur zusammen mit der XTF ausdrücklich auswählen.
2. Änderungen in SewerStudio speichern.
3. Auf der Exportseite **Neue XTF aus dem Projektstand – Exportumfang prüfen** wählen, dann **XTF erstellen**.
4. Die Vorschau prüfen. Fehlende Pflichtangaben oder ungültige Normwerte verhindern die Lieferung.

GeoShop-Objektakten und zusätzliche Aktenwerte aktivieren `DSS_2020_1_LV95`, Fassung
18.10.2023. Der bisherige SIA405-Weg bleibt für Projekte ohne solche Angaben erhalten.
Der getrennte Modus für reine Änderungsaufträge wird nicht in eine Voll-Lieferung umgedeutet.

Für die gesamte Originaldatei gibt es zusätzlich **SIA405-Lieferung bearbeiten …**.
Dieser freie Weg öffnet alle 22 gelieferten Objektarten in einer eigenen `.ssxtf`,
auch ohne Projektzeile. Anleitung und konkrete Grenzen: [Lieferungs-Editor](LIEFERUNGS-EDITOR.md).

## Umfang

Der feste Vertrag enthält **368 Attributdefinitionen in 23 Objektklassen**, erzeugt aus
den mitgelieferten offiziellen DSS-/Basismodellen. Das sind Normattribute, nicht 368
zusätzliche Eingabefelder. Alle skalaren GeoShop-Quellwerte dieser Klassen werden berücksichtigt,
auch ohne eigene sichtbare Eingabe.

- Haltung und Kanal: getrennte Bemerkungen, Material, Masse, effektive Länge, Nutzungsarten, Intervalle, Betreiber, Finanzierung und übrige gelieferte Attribute.
- Haltungspunkte: Koten, Höhengenauigkeit, Auslaufform, Anschlusslage und Knotenverknüpfung.
- Abwasserknoten/Bauwerk: Sohlen- und Rückstaukote, Zugänglichkeit und übrige gelieferte Attribute von Normschacht, Spezialbauwerk, Versickerungsanlage und Einleitstelle.
- Deckel und Einstiegshilfe: eigene Originalkennungen, Material, Form, Masse, Kote, Lüftung, Verschluss, Schlammeimer, Fabrikat und Bauwerksbezug.
- Inliner: Art, Bautechnik, Material und lichte Höhe mit Liner an der Haltung.
- Unterhalt/Sanierung: eigene Ereignisse, Art, Status, Zeitpunkt, Bemerkung, Kosten, ausführende Firma und Bezug zu einem oder mehreren Bauwerken. Reinigung bleibt Reinigung.
- Rohrprofil, hydraulische Geometrie und gelieferte Organisationsstammdaten.
- Förderaggregate, Absperr-/Drosselorgane, Streichwehre, Leapingwehre und Trockenwetterfallrohre:
  Originalobjekte, ihre modellierten Sachwerte und Beziehungen. Einbauten werden nach erneutem
  Abgleich in den bestehenden Masken bearbeitbar; auch Einstiegshilfen besitzen eigene Akten.

Aktuelle Aktenwerte und bearbeitete Bestandsfelder haben Vorrang. Pflichtfelder dürfen
nicht geleert werden. Optionales bleibt nach bewusstem Leeren im Export leer. Widersprüchliche
Eingaben für gemeinsam verwendete Objekte sperren die Ausgabe. Bei geändertem Profil entsteht
eine eigene Kennung; das gemeinsame Originalprofil wird nicht umgeschrieben.

Original-TIDs bleiben erhalten. Neue Deckel/Ereignisse erhalten stabile eigene TIDs aus
ihrer Projektidentität. Lokale Beleg-IDs werden nicht zu Katasterkennungen. Firmenreferenzen
stehen normgerecht direkt am Ereignis, Bauwerkszuordnungen als eigene Beziehungen.

Ab diesem Importstand bleiben auch XML-Geometrien einschliesslich Kreisbögen in der
Projektdatei erhalten. Der Export benötigt keinen Zugriff auf das Kundenoriginal.
Für früher importierte Projekte ist ein erneuter GeoShop-Abgleich nötig, wenn diese damals
noch nicht gespeicherten Geometrien mitgegeben werden sollen. Bei einer gedrehten Haltung
werden Punkte und Verlauf in Projektrichtung ausgegeben.

## Datenlücken

Ganze erfasste Akten ohne Exportanbindung oder ohne gültigen Projektbezug sperren die
Ausgabe. Der Fehler nennt **jede betroffene Akte samt Name und erfassten Feldern**, auch
bei mehr als 20 Akten. Unterstützt sind derzeit Haltung, Schacht, Deckel, Sanierung und
Unterhalt. Weitere angezeigte Aktenarten sind damit noch nicht als Normobjekte lieferbar.

Nicht übertragbare einzelne Angaben und nicht mitgelieferte Quellobjekte stehen mit
Name und Wert im Bericht. Ihre Anzahl erscheint bereits in der Vorschau, die ersten
Hinweise stehen direkt darunter; sämtliche Hinweise bleiben in den Details erhalten.
Die Option und der Bericht behaupten deshalb keinen pauschal vollständigen Projektstand.

Allgemeiner Unterhalt wird jetzt zusätzlich zu Sanierungen als eigene bearbeitbare Akte
importiert. Zwölf belegte Attribute gehen ins DSS-Unterhaltsobjekt: Bezeichnung, Art,
Status, Zeitpunkt, Dauer, Kosten, Bemerkung, Datengrundlage, Detaildaten, Ergebnis, Grund
und Ausführender. Originalkennungen und vorhandene Firmenbezüge bleiben erhalten;
erneuter Import erzeugt keine zweite Ereignisakte und überschreibt keine Handeingabe.
Auftrag/Nummer und WebGIS-interne Metadaten haben dadurch noch kein Normziel.

Die 68 WebGIS-Materialdetails sind ausdrücklich eingeordnet: 27 haben ein geprüftes
Normziel, 41 benötigen eine fachliche Entscheidung. Offene Werte sperren den Export,
statt ein altes Material unverändert auszugeben oder einen Sammelwert zu erfinden.
Sie bleiben im Projekt bearbeitbar. Siehe [Materialentscheidungen](reviews/2026-09-12-webgis/MATERIAL-ENTSCHEIDUNGEN.md).

WebGIS-interne Anzeige-/Verwaltungsfelder besitzen teilweise kein DSS-Ziel, etwa die
Hauptdeckel-Markierung und gewisse Hersteller-/Produktangaben am Ereignis. Sie werden nicht
in andere Normfelder geschrieben. Der Bericht nennt gesetzte Angaben ohne Zuordnung;
das begleitende Objektakten-JSON erhält die vollständigen Daten. Eine Jahreszahl allein
wird nicht zu einem erfundenen Ereignisdatum ergänzt.

Im projektbezogenen Export gilt: Fehlt schon in GeoShop eine vorgeschriebene Bezeichnung, dient die Original-TID als
technischer Name; der Bericht nennt dies. Fehlen externe Organisationen, bleiben belegte
Originalverweise erhalten. Diese Organisationen müssen im Zielbestand vorhanden sein.
Betreiber werden nicht aus Eigentümern und Firmen nicht aus Operateuren abgeleitet.
Ein Wechsel der Bauwerksklasse unter einer bestehenden Originalkennung wird nicht still ausgeführt.
Im freien Lieferungs-Editor werden fehlende Bezeichnungen als Korrekturbedarf gemeldet;
dort wird keine Ersatzbezeichnung aus einer TID eingesetzt.

Die Ausgabe enthält XTF, passende ILI-Modelle und über die Oberfläche ein ergänzendes
Objektakten-JSON. Hinweise bleiben zusätzlich im XTF-Kopf erhalten. Vorhandene Dateien
und Modelle mit anderem Inhalt werden nicht überschrieben.

## Prüfung und Technik

Haltungspunkte besitzen seit dem Dropdown-Abgleich vom 12.09.2026 eigene Akten.
Der erneute Katasterabgleich ergänzt sie auch bei bereits vorhandenen Quellbelegen.
Bezeichnung, Höhe, Lageanschluss, Auslaufform, Höhengenauigkeit und Bemerkung werden
auf den ursprünglichen Punkt geschrieben; gemeinsame oder widersprechende Eingaben
dürfen sich nicht still überschreiben. Lagebestimmung, Lagegenauigkeit und
Höhenbestimmung sowie die WebGIS-Witterung am allgemeinen Unterhalt besitzen kein
belegtes Zielfeld in diesem DSS-Vertrag. Der Bericht nennt gesetzte Angaben.

WebGIS-Originalcodes sind keine INTERLIS-Aufzählungswerte. Ergänzte Codes in Deckel-
und Sanierungslisten erweitern daher nicht automatisch den Normexport. Offene
Materialzuordnungen bleiben gesperrt; ihre Auswahleinträge bleiben erhalten.

Die Einbaumasken unterscheiden anhand der Originalklasse zwischen Streichwehr und
Leapingwehr sowie Einstiegshilfe und Trockenwetterfallrohr. Eine Auswahl darf diese
Klasse nicht unter derselben Originalkennung wechseln. Neue Einbauten ohne Original-
Zuordnung bleiben ausdrücklich gesperrt. Geerbte Schachtanzeigen lesen den zugehörigen
Schacht; sie ändern keine gleichnamigen Angaben des Einbaus. Die Einheit des WebGIS-
Arbeitspunkts `[m³]` wird nicht als DSS-Einheit `m³/s` ausgelegt. Nicht belegte Felder
bleiben mit Namen und Wert im Exportbericht sichtbar.

Der freie Lieferungs-Editor öffnet auch Netzknoten ohne Bauwerksbezug. Der bestehende
Projekt-Schachtabgleich verlangt weiterhin einen solchen Bezug. Neue Felder der Klassen
ARABauwerk, Abwasserbauwerk_Text, Haltung_Text und Messstelle sind zunächst im freien
Editor bearbeitbar. Er erhält Linien-/Flächengeometrien; deren grafische Bearbeitung
und die vollständigen WebGIS-Funktionen sind noch offen.
[Lieferungs-Editor und Gesamtabnahme](LIEFERUNGS-EDITOR.md).

Der unabhängige `ilivalidator 1.15.0` prüft erzeugte Dateien gegen die offiziellen lokalen
Modelle. Die vollständige synthetische Lieferung wird auch mit `--allObjectsAccessible`
geprüft. Der lesende Vergleich an der GeoShop-Quelle verändert weder Original noch WebGIS.
Ein tatsächlicher GEONIS-/FME-Rückimport ist noch nicht durchgeführt.

`Application/Xtf/Dss` enthält Zuordnung, Werteprüfung und Planung.
`Infrastructure/Import/Xtf/XtfDssWriter` schreibt XML. Quellmodelle liegen in
`Infrastructure/Import/Xtf/Models/Dss`. `tools/ErzeugeDssExportSchema.py` erzeugt den
Vertrag reproduzierbar und hält Modellprüfsummen fest. Keine neue NuGet-Abhängigkeit.

Nachweise: [Prüfbericht](reviews/2026-09-11-objektakten/DSS-EXPORT.md).

Nachtrag 14.09.2026: Auch der projektbezogene Änderungsmodus benutzt jetzt den
DSS-Verbund. Aktuelle Objektfelder, Listen und Angaben ohne Normziel werden im
separaten Eingabepaket mitgeliefert. Die bisherigen Aussagen «bleibt nur im Projekt»
gelten bei eingeschalteten Zusatzangaben nicht mehr. Die Modelle selbst bleiben
unverändert. [Aktuelle Funktion, Grenzen und Kontrollübersicht](XTF-EXPORT-KONTROLLE.md).
