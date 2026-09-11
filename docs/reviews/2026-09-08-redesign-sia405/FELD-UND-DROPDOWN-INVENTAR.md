# Feld- und Dropdown-Inventar

Ermittelt aus den lokalen Release-Programmdateien am 08.09.2026. Die Namen in den Normspalten sind exakte Exportwerte.
Anzeige mit Umlauten oder Leerzeichen ist zulässig, wenn die Zuordnung zum Normwert stimmt. Die Hauptbefunde stehen im PRUEFBERICHT.md.

## Alle 53 Standardfelder der Haltung

Alle 53 erscheinen im neu aufgebauten Standardformular. Benutzerdefinierte Ausblendungen wurden nicht als fehlende Felder gezählt.
Die Spalte „Revisionskarte“ nennt nur die direkte bestehende XTF-Revisionszuordnung. Separate Wege stehen in der letzten Spalte.

| Feldschlüssel | Beschriftung | Katalogtyp | Revisionskarte | Ergänzender Exportweg |
|---|---|---|---|---|
| NR | NR. | Int | — | Kein direkter Sachfeldexport in den geprüften Karten |
| Haltungsname | Haltungsname (ID) | Text | — | Bezeichnung / Objektzuordnung |
| Strasse | Strasse | Text | — | Neu-/Änderungslieferung: Standortname |
| Rohrmaterial | Rohrmaterial | Combo | Material | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| DN_mm | Lichte Höhe / DN mm | Int | Lichte_Hoehe | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Profiltyp | Profilform | Combo | Profiltyp | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Lichte_Breite_mm | Lichte Breite mm | Int | — | Rohrprofil.HoehenBreitenverhaeltnis aus Höhe/Breite |
| Nutzungsart | Nutzungsart | Combo | Nutzungsart_Ist | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Haltungslaenge_m | Haltungslänge m | Decimal | LaengeEffektiv | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Inspektionsrichtung | Inspektionsrichtung | Combo | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Primaere_Schaeden | Primäre Schäden | Multiline | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Zustandsklasse | Zustandsklasse | Combo | BaulicherZustand | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| VSA_Zustandsnote_D | VSA-Zustandsnote D | Decimal | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Pruefungsresultat | Prüfungsresultat | Text | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Referenzpruefung | Referenzpruefung | Combo | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Sanieren_JaNein | Sanieren Ja/Nein | Combo | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Empfohlene_Sanierungsmassnahmen | Empfohlene Sanierungsmassnahmen | Multiline | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Kosten | Kosten | Decimal | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Eigentuemer | Eigentümer | Text | — | EigentuemerRef über Organisation |
| Ausgefuehrt_durch | Ausgefuehrt durch | Combo | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Bemerkungen | Bemerkungen | Multiline | Bemerkung | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Link | Link | Text | — | Kein direkter Sachfeldexport in den geprüften Karten |
| Renovierung_Inliner_Stk | Renovierung Inliner Stk. | Int | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Renovierung_Inliner_m | Renovierung Inliner m | Decimal | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Anschluesse_verpressen | Anschlüsse verpressen | Int | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Reparatur_Manschette | Reparatur Manschette | Int | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Linerendmanschette_LEM | Linerendmanschette LEM | Int | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Reparatur_Kurzliner | Reparatur Kurzliner | Int | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Erneuerung_Neubau_m | Erneuerung Neubau m | Decimal | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Offen_abgeschlossen | offen/abgeschlossen | Combo | — | Kein direkter Sachfeldexport in den geprüften Karten |
| Datum_Jahr | Datum/Jahr | Text | — | Neu-/Änderungslieferung: Zustandserhebung_Jahr; volles Datum nur Zusatz |
| VSA_Zustandsnote_S | VSA-Zustandsnote S | Decimal | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| VSA_Zustandsnote_B | VSA-Zustandsnote B | Decimal | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| VSA_Geschaetzt | Note geschätzt | Text | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Gewaesserschutz | Gewässerschutz | Combo | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Grundwasserspiegel | Grundwasserspiegel | Combo | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| FunktionHierarchisch | Funktionale Hierarchie | Combo | FunktionHierarchisch | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Verbindungsart | Verbindungsart | Combo | Verbindungsart | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Bettung_Umhuellung | Bettung/Umhüllung | Combo | Bettung_Umhuellung | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Status | Status | Combo | Status | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Sanierungsbedarf | Sanierungsbedarf | Combo | Sanierungsbedarf | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| FunktionHydraulisch | Funktion hydraulisch | Combo | FunktionHydraulisch | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Lagebestimmung | Lagebestimmung | Combo | Lagebestimmung | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Baujahr | Baujahr | Int | Baujahr | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Bruttokosten | Bruttokosten (Kataster) | Decimal | Bruttokosten | Zusatzmodell, wenn kein Standardfeld geschrieben wird |
| Objekt_ID | Objekt-ID (Lisag) | Text | — | Kein direkter Sachfeldexport in den geprüften Karten |
| GEONIS_Kennung | GEONIS-Kennung | Text | — | Kennung im typisierten GEONIS-Objekt; Formular nur Anzeige |
| Datenherr | Datenherr | Text | — | DatenherrRef über Organisation |
| Datenlieferant | Datenlieferant | Text | — | DatenlieferantRef über Organisation |
| Organisation | Organisation | Text | — | Kein direkter Sachfeldexport in den geprüften Karten |
| Letzte_Aenderung | Letzte Änderung | Text | — | Kein direkter Sachfeldexport in den geprüften Karten |
| Aktualisierungsdatum | Aktualisierungsdatum | Text | — | Kein direkter Sachfeldexport in den geprüften Karten |
| Gefaelle_Promille | Gefälle ‰ | Decimal | — | Zusatzmodell, wenn kein Standardfeld geschrieben wird |

Hinweis: Prüfungsresultat, Eigentümer und Rohrmaterial erhalten ihren tatsächlichen Editor aus der verwalteten Dropdown-Regel; der reine Katalogtyp allein beschreibt diese Bedienung nicht vollständig.

## Schachtformular: tatsächlich erzeugte Editoren

| Feld | Auswahlfeld | Nur Ziffern |
|---|---|---|
| Funktion | nein | nein |
| Material | nein | nein |
| Schachtform | ja | nein |
| Eigentümer | ja | nein |
| Sanierungsbedarf | nein | nein |
| Status | nein | nein |
| Baujahr | nein | nein |
| Nutzungsart | nein | nein |
| Bauwerksart | ja | nein |
| Versickerungsart | ja | nein |
| Belastungsklasse | ja | nein |
| Bemerkungen | nein | nein |
| Inspektionsdatum | nein | nein |
| Primäre Schäden | nein | nein |
| Fotos | nein | nein |
| Ausgeführt durch | ja | nein |

## Exakte Werte der normbezogenen Haltungs-Dropdowns

### Rohrmaterial → Material

| Anzeige | XTF-Wert |
|---|---|
| andere | andere |
| Asbestzement | Asbestzement |
| Beton | Beton_unbekannt |
| Epoxydharz | Kunststoff_Epoxydharz |
| Faserzement | Faserzement |
| Gebrannte Steine | Gebrannte_Steine |
| GFK | kein Standardwert |
| Grauguss | Guss_Grauguss |
| Guss | kein Standardwert |
| Guss duktil | Guss_duktil |
| Hartpolyethylen | Kunststoff_Hartpolyethylen |
| Kunststoff unbekannt | Kunststoff_unbekannt |
| Normalbeton | Beton_Normalbeton |
| Ortsbeton | Beton_Ortsbeton |
| Polyester GUP | Kunststoff_Polyester_GUP |
| Polyethylen | Kunststoff_Polyethylen |
| Polypropylen | Kunststoff_Polypropylen |
| Polyvinylchlorid | Kunststoff_Polyvinilchlorid |
| Pressrohrbeton | Beton_Pressrohrbeton |
| Spezialbeton | Beton_Spezialbeton |
| Stahl | Stahl |
| Stahl rostfrei | Stahl_rostfrei |
| Steinzeug | Steinzeug |
| Ton | Ton |
| unbekannt | unbekannt |
| Zement | Zement |

### Profilform → Profiltyp

| Anzeige | XTF-Wert |
|---|---|
| Unbekannt | unbekannt |
| Kreisprofil | Kreisprofil |
| Eiprofil | Eiprofil |
| Maulprofil | Maulprofil |
| Offenes Profil | offenes_Profil |
| Rechteckprofil | Rechteckprofil |
| Spezialprofil | Spezialprofil |

### Nutzungsart → Nutzungsart_Ist

| Anzeige | XTF-Wert |
|---|---|
| andere | andere |
| Bachwasser | Bachwasser |
| entlastetes Mischabwasser | entlastetes_Mischabwasser |
| Industrieabwasser | Industrieabwasser |
| Mischabwasser | Mischabwasser |
| Niederschlagsabwasser | Niederschlagsabwasser |
| Reinabwasser | Reinabwasser |
| Schmutzabwasser | Schmutzabwasser |
| unbekannt | unbekannt |

### Zustandsklasse → BaulicherZustand

| Anzeige | XTF-Wert |
|---|---|
| 0 | Z0 |
| 1 | Z1 |
| 2 | Z2 |
| 3 | Z3 |
| 4 | Z4 |

### Funktionale Hierarchie → FunktionHierarchisch

| Anzeige | XTF-Wert |
|---|---|
| PAA.andere | PAA.andere |
| PAA.Gewaesser | PAA.Gewaesser |
| PAA.Hauptsammelkanal | PAA.Hauptsammelkanal |
| PAA.Hauptsammelkanal_regional | PAA.Hauptsammelkanal_regional |
| PAA.Liegenschaftsentwaesserung | PAA.Liegenschaftsentwaesserung |
| PAA.Sammelkanal | PAA.Sammelkanal |
| PAA.Sanierungsleitung | PAA.Sanierungsleitung |
| PAA.Strassenentwaesserung | PAA.Strassenentwaesserung |
| PAA.unbekannt | PAA.unbekannt |
| SAA.andere | SAA.andere |
| SAA.Liegenschaftsentwaesserung | SAA.Liegenschaftsentwaesserung |
| SAA.Sanierungsleitung | SAA.Sanierungsleitung |
| SAA.Strassenentwaesserung | SAA.Strassenentwaesserung |
| SAA.unbekannt | SAA.unbekannt |

### Verbindungsart → Verbindungsart

| Anzeige | XTF-Wert |
|---|---|
| andere | andere |
| Elektroschweissmuffen | Elektroschweissmuffen |
| Flachmuffen | Flachmuffen |
| Flansch | Flansch |
| Glockenmuffen | Glockenmuffen |
| Kupplung | Kupplung |
| Schraubmuffen | Schraubmuffen |
| spiegelgeschweisst | spiegelgeschweisst |
| Spitzmuffen | Spitzmuffen |
| Steckmuffen | Steckmuffen |
| Ueberschiebmuffen | Ueberschiebmuffen |
| unbekannt | unbekannt |
| Vortriebsrohrkupplung | Vortriebsrohrkupplung |

### Bettung/Umhüllung → Bettung_Umhuellung

| Anzeige | XTF-Wert |
|---|---|
| andere | andere |
| erdverlegt | erdverlegt |
| in_Kanal_aufgehaengt | in_Kanal_aufgehaengt |
| in_Kanal_einbetoniert | in_Kanal_einbetoniert |
| in_Leitungsgang | in_Leitungsgang |
| in_Vortriebsrohr_Beton | in_Vortriebsrohr_Beton |
| in_Vortriebsrohr_Stahl | in_Vortriebsrohr_Stahl |
| Sand | Sand |
| SIA_Typ1 | SIA_Typ1 |
| SIA_Typ2 | SIA_Typ2 |
| SIA_Typ3 | SIA_Typ3 |
| SIA_Typ4 | SIA_Typ4 |
| Sohlbrett | Sohlbrett |
| unbekannt | unbekannt |

### Status → Status

| Anzeige | XTF-Wert |
|---|---|
| ausser_Betrieb | ausser_Betrieb |
| in_Betrieb | in_Betrieb |
| tot | tot |
| unbekannt | unbekannt |
| weitere | weitere |

### Sanierungsbedarf → Sanierungsbedarf

| Anzeige | XTF-Wert |
|---|---|
| dringend | dringend |
| keiner | keiner |
| kurzfristig | kurzfristig |
| langfristig | langfristig |
| mittelfristig | mittelfristig |
| unbekannt | unbekannt |

### Funktion hydraulisch → FunktionHydraulisch

| Anzeige | XTF-Wert |
|---|---|
| andere | andere |
| Drainagetransportleitung | Drainagetransportleitung |
| Drosselleitung | Drosselleitung |
| Duekerleitung | Duekerleitung |
| Freispiegelleitung | Freispiegelleitung |
| Pumpendruckleitung | Pumpendruckleitung |
| Sickerleitung | Sickerleitung |
| Speicherleitung | Speicherleitung |
| Spuelleitung | Spuelleitung |
| unbekannt | unbekannt |
| Vakuumleitung | Vakuumleitung |
| Versickerungsleitung | Versickerungsleitung |

### Lagebestimmung → Lagebestimmung

| Anzeige | XTF-Wert |
|---|---|
| genau | genau |
| unbekannt | unbekannt |
| ungenau | ungenau |

### Normschacht.Funktion

| Anzeige | XTF-Wert |
|---|---|
| Absturzbauwerk | Absturzbauwerk |
| andere | andere |
| Be-/Entlüftung | Be_Entlueftung |
| Behandlungsanlage | Behandlungsanlage |
| Bodenablauf | Bodenablauf |
| Dachwasserschacht | Dachwasserschacht |
| Einlaufschacht | Einlaufschacht |
| Entwässerungsrinne | Entwaesserungsrinne |
| Entwässerungsrinne mit Schlammsack | Entwaesserungsrinne_mit_Schlammsack |
| Fettabscheider | andere |
| Geleiseschacht | Geleiseschacht |
| Kombischacht | Kombischacht |
| Kontrollschacht | Kontroll_Einsteigschacht |
| Pumpwerk | Pumpwerk |
| Regenüberlauf | Regenueberlauf |
| Schlammsammler | Schlammsammler |
| Schwimmstoffabscheider | Schwimmstoffabscheider |
| Sickerschacht | andere |
| Spezialbauwerk | andere |
| Spülschacht | Spuelschacht |
| Trennbauwerk | Trennbauwerk |
| unbekannt | wird nicht geschrieben |
| Vorbehandlungsanlage | Vorbehandlungsanlage |
| Ölabscheider | Oelabscheider |

### Normschacht.Material

| Anzeige | XTF-Wert |
|---|---|
| andere | andere |
| Beton | Beton |
| Fertigbetonelement | Beton |
| Gemauert | andere |
| GFK | Kunststoff |
| Kunststoff | Kunststoff |
| Ortsbeton | Beton |
| Polyethylen | Kunststoff |
| Polypropylen | Kunststoff |
| unbekannt | wird nicht geschrieben |

### Spezialbauwerk.Funktion

`abflussloseGrube`, `Absturzbauwerk`, `Abwasserfaulraum`, `andere`, `Be_Entlueftung`, `Behandlungsanlage`, `Duekerkammer`, `Duekeroberhaupt`, `Faulgrube`, `Fettabscheider`, `Gelaendemulde`, `Geschiebefang`, `Guellegrube`, `Havariebecken`, `Klaergrube`, `Kombischacht`, `Kontroll_Einsteigschacht`, `Oelabscheider`, `Pumpwerk`, `Regenbecken_Durchlaufbecken`, `Regenbecken_Fangbecken`, `Regenbecken_Fangkanal`, `Regenbecken_Regenklaerbecken`, `Regenbecken_Regenrueckhaltebecken`, `Regenbecken_Regenrueckhaltekanal`, `Regenbecken_Stauraumkanal`, `Regenbecken_Verbundbecken`, `Regenueberlauf`, `Schwimmstoffabscheider`, `seitlicherZugang`, `Spuelschacht`, `Trennbauwerk`, `unbekannt`, `Vorbehandlungsanlage`, `Wirbelfallschacht`

### Versickerungsanlage.Art

`andere_mit_Bodenpassage`, `andere_ohne_Bodenpassage`, `Flaechenfoermige_Versickerung`, `Kieskoerper`, `Kombination_Schacht_Strang`, `MuldenRigolenversickerung`, `unbekannt`, `Versickerung_ueber_die_Schulter`, `Versickerungsbecken`, `Versickerungsschacht`, `Versickerungsstrang_Galerie`

## Modellvergleich: angebotene Exportwerte

| Feld | Normwerte 2021 | Fehlende Exportwerte | Zusätzliche ungültige Exportwerte |
|---|---:|---|---|
| Rohrmaterial | 24 | keine | keine |
| Profiltyp | 7 | keine | keine |
| Nutzungsart | 9 | keine | keine |
| Zustandsklasse | 6 | unbekannt | keine |
| FunktionHierarchisch | 14 | keine | keine |
| Verbindungsart | 13 | keine | keine |
| Bettung_Umhuellung | 14 | keine | keine |
| Status | 5 | keine | keine |
| Sanierungsbedarf | 6 | keine | keine |
| FunktionHydraulisch | 12 | keine | keine |
| Lagebestimmung | 3 | keine | keine |
| Schacht.Funktion | 22 | Fettabscheider, unbekannt | keine |
| Schacht.Material | 4 | unbekannt | keine |
| Spezialbauwerk.Funktion | 34 | keine | Kombischacht |
| Versickerungsanlage.Art | 11 | keine | keine |

Die fehlenden unbekannt-Werte und Fettabscheider sind bewusste Bestandsregeln; siehe Bericht. Kombischacht ist in Spezialbauwerk erst in der geprüften Fassung 2025 enthalten. Die Werte in Normschacht sind davon zu unterscheiden.

## Zusätzliche Normfelder ausserhalb des heutigen Haltungs-Standardformulars

Die komplette SIA-405-Struktur ist umfangreicher als die Inspektionsmaske. Beispiele: Kanal.Nutzungsart_geplant, Rohrlaenge, Sickerung, Spuelintervall; Haltung.Reliner_Art, Reliner_Bautechnik, Reliner_Material, Reliner_Nennweite; Deckel.Kote, Deckelform, Entlueftung und Material sowie Sohlenkote am Abwasserknoten.
Diese Angaben sind in den untersuchten Klassen optionale Attribute. Ihr Fehlen beweist allein keine ungültige XTF. Für einen vollständigen Katastereditor müssen die tatsächlich benötigten Objektklassen und Lieferanforderungen festgelegt werden. Gleichnamige freie Projektfelder ohne bestätigten Exportweg ersetzen das nicht.

## Herkunftsnachweis der gelesenen Modelle

| Datei | SHA-256 |
|---|---|
| Base_2020.ili | 473a5de777d40a905c18a49c99bf434a2bfdb06f759366fd349dde9c7eaa66bf |
| Base_2020_1.ili | d368a421e126dd7aba8d4cac27761089469590a4d0fcaed4d47f55241f0a63c6 |
| SIA405_2020.ili | 7e8968ad64cd3c3eaff2e79520d8d5276441d7fcd1b86fa391be43d407a295fb |
| SIA405_2020_1.ili | dfb6e30f4ee8764f609dbe4c662103b60fe096681218fa6c16a2b5524dce6cc0 |
| SIA405_2021.ili | 97ffeab1c6ac6a515beb4188a3a86710184f25e9671a96ec556102bbb84d567d |
