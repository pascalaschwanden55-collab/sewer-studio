# Vollständige XTF aus GeoShop-Objektakten

Stand: 11.09.2026. Nova-Farben, Karten und Bedienaufbau bleiben bestehen.

## Bedienung

1. GeoShop-Abgleich ausführen und die gewünschten Angaben übernehmen. Eine Eigentümer-JSON nur zusammen mit der XTF ausdrücklich auswählen.
2. Änderungen in SewerStudio speichern.
3. Auf der Exportseite **Vollständige neue XTF mit allen belegten Normfeldern** wählen, dann **XTF erstellen**.
4. Die Vorschau prüfen. Fehlende Pflichtangaben oder ungültige Normwerte verhindern die Lieferung.

GeoShop-Objektakten und zusätzliche Aktenwerte aktivieren `DSS_2020_1_LV95`, Fassung
18.10.2023. Der bisherige SIA405-Weg bleibt für Projekte ohne solche Angaben erhalten.
Der getrennte Modus für reine Änderungsaufträge wird nicht in eine Voll-Lieferung umgedeutet.

## Umfang

Der feste Vertrag enthält **248 Attributdefinitionen in 14 Objektklassen**, erzeugt aus
den mitgelieferten offiziellen DSS-/Basismodellen. Das sind Normattribute, nicht 248
zusätzliche Eingabefelder. Alle skalaren GeoShop-Quellwerte dieser Klassen werden berücksichtigt,
auch ohne eigene sichtbare Eingabe.

- Haltung und Kanal: getrennte Bemerkungen, Material, Masse, effektive Länge, Nutzungsarten, Intervalle, Betreiber, Finanzierung und übrige gelieferte Attribute.
- Haltungspunkte: Koten, Höhengenauigkeit, Auslaufform, Anschlusslage und Knotenverknüpfung.
- Abwasserknoten/Bauwerk: Sohlen- und Rückstaukote, Zugänglichkeit und übrige gelieferte Attribute von Normschacht, Spezialbauwerk, Versickerungsanlage und Einleitstelle.
- Deckel und Einstiegshilfe: eigene Originalkennungen, Material, Form, Masse, Kote, Lüftung, Verschluss, Schlammeimer, Fabrikat und Bauwerksbezug.
- Inliner: Art, Bautechnik, Material und lichte Höhe mit Liner an der Haltung.
- Unterhalt/Sanierung: eigene Ereignisse, Art, Status, Zeitpunkt, Bemerkung, Kosten, ausführende Firma und Bezug zu einem oder mehreren Bauwerken. Reinigung bleibt Reinigung.
- Rohrprofil, hydraulische Geometrie und gelieferte Organisationsstammdaten.

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

WebGIS-interne Anzeige-/Verwaltungsfelder besitzen teilweise kein DSS-Ziel, etwa die
Hauptdeckel-Markierung und gewisse Hersteller-/Produktangaben am Ereignis. Sie werden nicht
in andere Normfelder geschrieben. Der Bericht nennt gesetzte Angaben ohne Zuordnung;
das begleitende Objektakten-JSON erhält die vollständigen Daten. Eine Jahreszahl allein
wird nicht zu einem erfundenen Ereignisdatum ergänzt.

Fehlt schon in GeoShop eine vorgeschriebene Bezeichnung, dient die Original-TID als
technischer Name; der Bericht nennt dies. Fehlen externe Organisationen, bleiben belegte
Originalverweise erhalten. Diese Organisationen müssen im Zielbestand vorhanden sein.
Betreiber werden nicht aus Eigentümern und Firmen nicht aus Operateuren abgeleitet.
Ein Wechsel der Bauwerksklasse unter einer bestehenden Originalkennung wird nicht still ausgeführt.

Die Ausgabe enthält XTF, passende ILI-Modelle und über die Oberfläche ein ergänzendes
Objektakten-JSON. Hinweise bleiben zusätzlich im XTF-Kopf erhalten. Vorhandene Dateien
und Modelle mit anderem Inhalt werden nicht überschrieben.

## Prüfung und Technik

Der unabhängige `ilivalidator 1.15.0` prüft erzeugte Dateien gegen die offiziellen lokalen
Modelle. Die vollständige synthetische Lieferung wird auch mit `--allObjectsAccessible`
geprüft. Der lesende Vergleich an der GeoShop-Quelle verändert weder Original noch WebGIS.
Ein tatsächlicher GEONIS-/FME-Rückimport ist noch nicht durchgeführt.

`Application/Xtf/Dss` enthält Zuordnung, Werteprüfung und Planung.
`Infrastructure/Import/Xtf/XtfDssWriter` schreibt XML. Quellmodelle liegen in
`Infrastructure/Import/Xtf/Models/Dss`. `tools/ErzeugeDssExportSchema.py` erzeugt den
Vertrag reproduzierbar und hält Modellprüfsummen fest. Keine neue NuGet-Abhängigkeit.

Nachweise: [Prüfbericht](reviews/2026-09-11-objektakten/DSS-EXPORT.md).
