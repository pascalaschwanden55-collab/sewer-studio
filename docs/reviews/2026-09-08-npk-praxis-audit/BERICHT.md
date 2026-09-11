# NPK- und Offertenprüfung vom 08.09.2026

**Die Umsetzung ist als vereinfachte Kostenschätzung brauchbar angelegt, aber noch nicht durchgehend verlässlich.** Viele Leistungen und mehrere Preisansätze passen zur Praxis. Bestätigt sind jedoch Fehler bei Endanschlüssen, Fräsmengen, Abnahmereinigung und der Excel-Berechnung. Sie lassen sich nicht mit einer erlaubten Preisungenauigkeit erklären.

Massstab ist die nachträgliche Vorgabe des Auftraggebers: Nähe zur tatsächlichen Offertenpraxis genügt; keine zwanghafte Nachbildung jeder NPK-Unterposition. Deshalb gelten bewusst vereinbarte Meterpreise und zusammengefasste Leistungen nicht als Fehler. Produktcode und Kundenoriginale wurden bei diesem Audit nicht geändert.

## Grundlage und Grenzen

- 44 ausgelieferte Katalogeinträge, 20 Massnahmenvorlagen; davon 36 Einträge mit Revisionsnummer und 8 ohne solche Nummer.
- Katalogquelle: `D:/Fachwissen/Revision_NPK135.pdf`, 259 PDF-Seiten, Revisionsentwurf D/V27, Textstand 16.01.2026. Der zuerst genannte Pfad `Revision/_NPK135.pdf` existiert nicht.
- 43 ausgewählte PDFs aus den beiden genannten Ordnern erschlossen, einschliesslich Mehrfachfassungen, 3 Präzisierungen und 2 Kontextdateien. Das sind **nicht 43 unabhängige Angebote**. Für 20 Dateien wurden 176 Seiten lokal per OCR gelesen. Die entscheidenden Preis- und Vorgabenseiten wurden zusätzlich am Bild kontrolliert.
- Schwerpunkt des Preisvergleichs: Fretz, GKS und ITS für Bürglen 5.01, 5.13 und 5.14 sowie Fretz-Einzelangebote 2026 und PRO Liner AN-00747. Auch die mitgelieferten Schlussrechnungen wurden zur Unterscheidung von Angebot, Ausmass und Nachträgen herangezogen.
- Keine vollständige Rechnungsprüfung jeder Lieferantenrechnung und keine Neuberechnung eines echten Kundenprojekts. Die vorhandenen Excel-Vergleichsdateien und sämtliche technischen Beilagen wurden nicht einzeln geprüft. Persönliche Katalogüberschreibungen sind nicht Teil des Preisvergleichs.
- Preise unten sind positionsbezogene Ansätze **vor allgemeinen Rabatten, Skonto und MwSt.**; bei getrennten Linerpreisen wurde Lieferung + Einbau + genannte Folie addiert. Unterschiedliche Wanddicken, Produkte, Längen und Baustellenbedingungen bleiben relevant.
- Die offizielle [CRB-Überarbeitungsübersicht](https://www.crb.ch/de/normen-standards/normpositionen/vernehmlassung-uberarbeitung) führt NPK 135 unter Ausgabe 2027. Die vorgelegte Revision ist deshalb keine geeignete Begründung, sämtliche individuellen Nummern aus den Offerten von 2023 bis 2026 als falsch zu verwerfen.

## Bestätigte Fehler und wichtige Lücken

### F1 – Hoch: Endanschlüsse unter DN 200 verschwinden

`MeasureRuleService` setzt Endmanschetten bei DN < 200 auf Menge 0 und wählt sie ab. Das trifft auch manuell erfasste Mengen beim Anwenden dieser Regel. Im Katalog gibt es gleichzeitig einen Preis für DN 150.

**Gegenbeleg aus der Praxis:** PRO Liner AN-00747 enthält bei DN 150 zwei Quicklock-Endmanschetten zu je CHF 395; ITS Bürglen 5.14 enthält Endmanschetten bis DN 200 und zusätzlich einen eigenen Open-End-Abschluss. Fretz Seilergasse enthält vier angepasste Linerenden für zwei kurze Leitungen DN 125/150.

**Laufzeitnachweis:** Synthetische Haltung DN 150, Nadelfilz: LEM wird auf 0 gesetzt. Eine alternative Position für Mörtelanbindung oder Open-End-Abschluss wird nicht erzeugt.

**Korrekturziel:** Abschlussart und Anzahl auswählbar machen, auch unter DN 200. Zwei Enden können ein Vorschlag bleiben; die Methode muss zur Haltung passen. Die Normrevision 621 unterscheidet selbst metallische und manuelle Anschlüsse.

### F2 – Hoch: Die separate Abnahmereinigung fehlt

Die Präzisierungen vom 02.04.2026 verlangen fünf getrennte Leistungen: Vorreinigung, TV vorher, Abnahmereinigung, TV nachher, Fräsen. Der ausgelieferte Sanierungsablauf enthält nur die erste Reinigung und beide TV-Aufnahmen. Eine eigene Position für die Reinigung vor Abnahme fehlt in Katalog und Standardvorlagen.

**Grössenordnung:** In Bürglen 5.01 sind 550 m Abnahmereinigung zu CHF 5/m angeboten: CHF 2’750 vor Rabatt/MwSt. Diese Grössenordnung fehlt, wenn dieselbe Leistung allein mit dem heutigen Standardpaket nachgebildet wird. Kein behaupteter Ist-Verlust im Kundenprojekt: dessen gespeicherte Anpassungen wurden nicht ausgewertet.

**Korrekturziel:** Abnahmereinigung als eigene auswählbare Leistung ergänzen. Bei einer ausdrücklich alles enthaltenden Pauschale Doppelzählung vermeiden.

### F3 – Hoch: Fräsmeter werden im Export umgedeutet; Nummer hängt von Reihenfolge ab

Im Kostenfenster ist Fräsen eine Meterposition. Beim Export wird `Meter × Meterpreis / aktueller Stundenpreis` gerechnet. Dadurch ändern sich die ausgewiesenen Stunden allein durch eine Katalogpreisänderung, ohne Änderung des Arbeitsumfangs. Die Präzisierungen und Fretz-Bürglen-Angebote verlangen hier ausdrücklich Meter.

**Reproduziert:** 10 m × CHF 29 ergeben bei CHF 290/h eine Stunde; bei CHF 580/h nur eine halbe Stunde. Der Geldbetrag bleibt CHF 290. Das ist eine Preisumrechnung, kein gemessener Zeitaufwand.

Zusätzlich werden Meter-Fräsen und Stunden-Roboter zu einer Zeile vereinigt, aber deren NPK-Metadaten stammen von der zuerst gelesenen Zeile. Dieselben zwei Leistungen ergeben je nach Reihenfolge **321.111** oder **311.111**; auch die D/16-Nummer wechselt. 321 bezeichnet in der Revision Reprofilierung, nicht das Entfernen von Hindernissen.

**Korrekturziel:** Vereinbarte Einheit erhalten. Meter- und Stundenangebote getrennt führen oder ausdrücklich gewählte, nachvollziehbare Kalkulationsart verwenden. Eine Positionsnummer darf nie von der Reihenfolge der Haltungen abhängen. Die pauschale Vorbelegung aller Fräsmeter mit der ganzen Haltungslänge muss als Vorschlag korrigierbar bleiben: 550 m Reinigung bedeuten in der Präzisierung nur 100 m Fräsen.

### F4 – Hoch: Excel kann bei einem Nullpreis ein anderes Total als CSV/PDF bilden

Der Zusammenfasser ignoriert Preise von 0 bei der Prüfung auf verschiedene Einheitspreise. Die Mengen der betreffenden Zeilen werden trotzdem addiert. Excel bildet dann Menge × einen positiven Einheitspreis, während CSV/PDF das tatsächlich aufsummierte Total verwenden.

**Reproduziert:** 10 m zu CHF 5 plus 10 m zu CHF 0 → zusammen 20 m, angezeigter EP CHF 5, tatsächliches Total CHF 50. Im erzeugten Excel steht `E9=20`, `G9=5`, Formel `H9=E9*G9`: beim Berechnen CHF 100. CSV/PDF verwenden CHF 50. Das Beispiel ist rein synthetisch.

**Korrekturziel:** Nullpreise als eigene Preissituation behandeln. Für fehlende Preise einen klaren Status vorsehen; eine ausdrücklich kostenlose Leistung darf ebenfalls kein falsches Total verursachen. Dass ein Angebot zum Ausfüllen noch keinen Preis hat, ist dagegen normal.

### F5 – Wichtig: Baustelleneinrichtung wird pro Haltung vervielfacht

Jedes Linerpaket erzwingt seine Installation; die projektweite Liste summiert sie. Zwei Nadelfilz-Haltungen ergeben in der Probe 2 × CHF 500. Eine gemeinsame Baustelle oder Einbauetappe ist im Mengenmodell nicht vorhanden.

Die Offerten unterscheiden Einrichtung für die ganze Baustelle von Arbeit je Haltung. Beispielsweise führt GKS Bürglen 5.01 eine GFK-Anlage einmal mit CHF 2’500 auf. Die Anwendung würde bei acht GFK-Haltungen standardmässig 8 × CHF 1’500 ansetzen. Das Beispiel veranschaulicht den Unterschied der Berechnungsart und ist keine Neuberechnung dieser Offerte.

**Bewertung nach Praxisvorgabe:** Ein verteilter Richtbetrag je Haltung ist für eine grobe Schätzung denkbar, muss dann aber so bezeichnet sein. Als automatisch ermittelte Anzahl ganzer Baustelleneinrichtungen ist er irreführend.

**Korrekturziel:** Mengenbezug „Projekt / Etappe / Haltung“ festlegen und gemeinsame Einrichtungen einmal zählen. Ein echter zusätzlicher Antransport bleibt separat möglich.

### F6 – Wichtig: Schachtliner hat widersprüchliche Mengen und Einheiten

Der Standardschachtliner enthält Folie in m², Lieferung in m und Einbau in m. Bei Änderung der Hauptmenge auf 3 werden nur 3 m Einbau erzeugt; Lieferung bleibt 1 m, Folie 1 m², Anschlüsse und Enden je 1. Schachtdurchmesser, Tiefe und Anschlusszahl werden nicht in die Factory gegeben.

In der Revision wird die Schutzfolie nach Länge in m abgerechnet (711.101), der hier vorgesehene Schachtliner bei Lieferung und Einbau hingegen nach Anzahl Schächte und nach DN/Höhe (712/716). Selbst bei einer bewusst vereinfachten Meterkalkulation passen 1 m Lieferung und 3 m Einbau ohne Erläuterung nicht zusammen.

**Zusätzliche Preisgrenze:** Alle 12 ausgelieferten `SCHACHT_*`-Einträge im Kostenkatalog haben CHF 0 als Platzhalter. Der andere Schacht-Massnahmenkatalog besitzt wiederum eigene Richtpreise. Somit hängt das Startpreisverhalten vom gewählten Bearbeitungsweg ab; eine komplette Schachtsanierung ist mit den NPK-Defaults allein nicht verlässlich kalkuliert.

**Korrekturziel:** Ein konsistentes Schachtpaket mit getrennten Mengen für Schachtzahl, Tiefe/Folie, Anschlüsse und Enden. Unbekannte Preise kenntlich machen und Preisquellen abstimmen. Bestehende manuelle Kundeneingaben erhalten.

### F7 – Wichtig: Der Leistungsumfang des Linerpreises ist nicht eindeutig

Die App führt pro Linerart nur eine Meter-Hauptposition unter 612. Die Revision trennt Lieferung (612), Einbau (614) und gegebenenfalls weitere Leistungen. Die Offerten verwenden **beide** Modelle: Fretz Seilergasse fasst Lieferung, Imprägnieren und Einbau zusammen; GKS Bürglen und Fretz Flüelerstrasse trennen Material und Einbau.

**Bewertung nach Praxisvorgabe:** Der zusammengefasste App-Preis ist akzeptabel, wenn er ausdrücklich Lieferung und Einbau umfasst. Es wäre falsch, jetzt ohne Preisanpassung einfach eine weitere volle Einbauposition hinzuzufügen. Bei Übernahme eines reinen Materialpreises in die bestehende Hauptposition fehlen sonst die Einbaukosten.

**Korrekturziel:** Preisumfang klar beschriften, z.B. „Liner liefern und einbauen“, mit auswählbaren Zusatzleistungen. Bei getrennten Angeboten beide Preisanteile übernehmen. Gleiche Leistung nicht zweimal verrechnen. Open-End-Zuschläge, Preliner, durchgezogene Schächte und manuelle Endanbindungen sind heute nicht vollständig als passende Standardoptionen vorhanden.

### F8 – Mittel: Nummern und Durchmesser liefern keine verlässliche Normzuordnung

Die NPK-Nummer hängt nur am Katalogschlüssel. Eine andere Nennweite ändert zwar teilweise den Preis, aber nicht die Nummer. Die Probe DN 400 exportiert z.B. 612.111, obwohl diese Revisionsgruppe für Hauptkanäle bis DN 250 gilt. Bei Positionen vom Typ `Fixed` wird sogar die DN-Angabe aus der zusammengefassten Zeile entfernt.

Weitere konkrete Befunde: 10 hinterlegte Nummern sind in der Revision Gruppenüberschriften; 221.102 (zweimal verwendet), 712.101 und 716.101 sind dort auch unter Berücksichtigung der angegebenen Wiederholungsbereiche keine passenden Unterpositionen. 725.101 und 612.113 sind dagegen über „bis … wie …“ abgedeckt und dürfen nicht allein wegen fehlender Einzelzeile als nicht existent gelten.

**Bewertung nach Praxisvorgabe:** Vereinfachte firmen- oder projektspezifische Nummern sind kein Grund, das ganze System umzubauen. Sie müssen aber als solche erkennbar sein. Revisionsnummer, D/16-Vergleichsnummer und individuelle Position sind keine austauschbaren Identitäten. Die PDF-Offerte zeigt zudem die D/16-Spalte nicht, während Excel/CSV sie ausgeben.

**Korrekturziel:** Zuerst falsche Leistungszuordnungen entfernen und Ausgabeversion kennzeichnen. Exakte, DN-abhängige Unterpositionen nur dann behaupten, wenn DN, Verfahren und erforderliche Angaben tatsächlich passen. Eine strenge Normfreigabe wird hier nicht erteilt.

## Wie nahe sind die Preise an der Praxis?

Diese Auswahl vergleicht ähnliche Leistungsumfänge; sie ist keine statistische Preisprognose. Die folgenden Abweichungen beziehen sich nur auf die jeweilige Position, nicht auf ganze Projekte.

| Leistung | App-Standard | Beleg aus Offerte | Einordnung |
|---|---:|---:|---|
| Vorreinigung, je m | CHF 5.00 | Fretz Bürglen: CHF 5.00 | Genau der angebotene Meterpreis |
| TV vorher/nachher, je m | CHF 3.00 | Fretz Bürglen: CHF 3.00 | Genau der angebotene Meterpreis |
| Fräsen, je m | CHF 29.00 | Fretz Bürglen: CHF 29.00 | Preis passt; Exporteinheit fehlerhaft |
| Kanalroboter, je h | CHF 290.00 | GKS Bürglen: CHF 295.00 | Rund 2 % darunter |
| Anschluss auffräsen, je St | CHF 150.00 | GKS 5.01: 150; Fretz 5.01: 136.40; ITS 5.01: 250 | Plausibler Ansatz, erhebliche Anbieterstreuung |
| Anschluss in Liner einbinden, je St | CHF 800.00 | GKS 5.01: 700; Fretz 5.01: 707.80; ITS 5.01: 900 | Innerhalb dieser Angebotsbandbreite |
| Nadelfilz DN 150, Lieferung + Einbau, je m | CHF 250.00 | Fretz Seilergasse: CHF 222.70 | Rund 12 % darüber; Wanddicke dort 4 mm |
| Nadelfilz bis DN 200, Lieferung + Einbau + Folie, je m | CHF 240–270 je DN | ITS 5.14: 225 + 2.40 = CHF 227.40 | Grössenordnung plausibel; Wanddicke dort 4.5 mm |
| GFK DN 250, Lieferung + Einbau + Folie, je m | CHF 200.00 | GKS 5.01: 75.90 + 55 + 1.50 = CHF 132.40 | Rund 51 % darüber; GKS nennt 3 mm Verbundwanddicke |
| GFK DN 300, Lieferung + Einbau + Folie, je m | CHF 220.00 | GKS 5.01: 79.75 + 55 + 1.50 = CHF 136.25 | Rund 61 % darüber; kein bloss kleiner Preisunterschied |
| GFK DN 400, Lieferung + Einbau + Folie, je m | CHF 275.00 | ITS 5.13: 93.02 + 95 + 3.60 = CHF 191.62 | Rund 44 % darüber; dort 4 mm; Einbauzeile umfasst 295 m |
| GFK DN 350, Lieferung + Einbau + Folie, je m | CHF 230.00 | Fretz Flüelerstrasse: 121.55 + 160.30 + 8.90 = CHF 290.75 | Rund 21 % darunter; kurze 19-m-Baustelle, 3.8 mm |
| Endmanschette DN 150, je St | CHF 420.00, automatisch abgewählt | PRO Liner: CHF 395.00 | Preis nahe; fehlende Leistung ist das Problem |

**Schluss daraus:** Die Hilfsarbeiten und Nadelfilzansätze sind vielfach plausibel. Die GFK-Preise sind nicht einheitlich „nahe“: grössere Lose werden teils deutlich überschätzt, kurze Einzelbaustellen können unterschätzt werden. Ohne Angaben zu Preisumfang, Wanddicke und Mindestaufwand wäre eine pauschale Preissenkung falsch. Erst Mengen- und Paketfehler korrigieren, dann Richtpreise nach kleinen/grösseren Aufträgen prüfen.

Die gesamten Offertsummen aus Bürglen sind kein sauberer direkter Genauigkeitsmassstab: In Zone 5.01 stehen bei Fretz 446 m Lieferung/Einbau, bei GKS ungefähr 403 m, bei ITS 399 m Einbau. Auch Anschlusszahlen unterscheiden sich (9 / 11 / 14). Das muss je Haltung abgeglichen werden, bevor man die Totale als denselben Leistungsumfang behandelt. Ein Unterschied in der Offerte ist damit noch kein nachgewiesener Unternehmerfehler.

## Was nachweislich funktioniert

- Alle Vorlagenschlüssel finden einen Katalogeintrag; keine kaputten Verknüpfungen unter den 20 ausgelieferten Vorlagen.
- Grundsätzlich getrennte Linerarten und DN-Preisgruppen; positive unterschiedliche Preise werden als variabel behandelt.
- Die aktuellen CSV-Nummern bleiben als Text erhalten; Schweizer Dezimalpunkt und Rundung der Zwischentotale sind gegenüber dem alten Juni-Audit korrigiert.
- Preisübernahme vom nächstliegenden DN wird in der Probe sichtbar ausgewiesen, nicht still verschwiegen.
- Die meisten benötigten Hauptarbeiten sind vorhanden: Liner, Kurzliner, Manschetten, Anschlussarbeiten, Reinigung/TV, Wasserhaltung und verschiedene Schachtmassnahmen.
- 130 gezielte Infrastrukturtests und 5 UI-Tests erfolgreich, 0 fehlgeschlagen, 0 übersprungen. Die neuen Laufzeitproben belegen trotzdem die oben beschriebenen Lücken; grüne Tests sind keine fachliche NPK-Abnahme.

## Sinnvolle Reihenfolge der Korrekturen

1. Excel-Total und Fräsexport korrigieren; vorhandene Mengen und Preise erhalten.
2. Endanschlüsse auch unter DN 200 sowie Abnahmereinigung ergänzen.
3. Globale Einrichtungen und mengenabhängige Schachtpakete eindeutig behandeln.
4. Preisumfang der Liner sowie optionale Zusatzleistungen verständlich beschriften.
5. Danach Richtpreise anhand vergleichbarer Offerten nachführen und die verbleibenden Normnummern bereinigen.

Für die gewünschte pragmatische Lösung braucht es keinen kompletten neuen NPK-Editor. Es braucht eindeutige Leistungen, richtige Einheiten, nachvollziehbare Mengen und eine Ausgabe, deren Total mit der Kalkulation übereinstimmt.

## Belegstellen

| Befund | Umsetzung | Quelle / Nachweis |
|---|---|---|
| F1 | [MeasureRuleService.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/MeasureRuleService.cs:139>) | [O28: an-00747 abwasser uri,.pdf](<D:/Fachwissen/Offerten/an-00747 abwasser uri,.pdf>), PDF-Seite 7; [O40: Sanierungsmassnahmen_Kanalisation_5.14.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.14_Grund_Löwenmatt/ITS/Sanierungsmassnahmen_Kanalisation_5.14.pdf>), PDF-Seite 10; [O16: Offerte_26100038-1-0_Sanierung, Seilergasse 17, Altdorf UR.pdf](<D:/Fachwissen/Offerten/Leitungen/Offerte_26100038-1-0_Sanierung, Seilergasse 17, Altdorf UR.pdf>), PDF-Seite 3 |
| F2 | [measure_templates.json](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Config/measure_templates.json:10>) | [O41: Präzisierung_Pos_2.1_Zone_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Präzisierung/Präzisierung_Pos_2.1_Zone_5.01.pdf>), PDF-Seite 1; [O42: Präzisierung_Pos_2.1_Zone_5.13.pdf](<D:/Fachwissen/Offertenvergleich/Präzisierung/Präzisierung_Pos_2.1_Zone_5.13.pdf>), PDF-Seite 1; [O43: Präzisierung_Pos_2.1_Zone_5.14.pdf](<D:/Fachwissen/Offertenvergleich/Präzisierung/Präzisierung_Pos_2.1_Zone_5.14.pdf>), PDF-Seite 1; [O29: Kanalsanierung_Bürglen_Zone_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.01_Farb-Grossgrund/Fretz/Kanalsanierung_Bürglen_Zone_5.01.pdf>), PDF-Seite 5 |
| F3 | [ProjectPositionAggregator.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/ProjectPositionAggregator.cs:68>) | [O41: Präzisierung_Pos_2.1_Zone_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Präzisierung/Präzisierung_Pos_2.1_Zone_5.01.pdf>), PDF-Seite 1; Revision PDF 64 und 67; Laufzeitprobe `fraesen_zuerst`, `roboter_zuerst`, `fraesen_stundensatz_verdoppelt` |
| F4 | [ProjectPositionAggregator.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/ProjectPositionAggregator.cs:146>); [NpkLeistungsverzeichnisExcelExporter.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/NpkLeistungsverzeichnisExcelExporter.cs:213>) | `probe-nullpreis.xlsx`, interner Reiter E9/G9/H9; `probe-ergebnis.json` |
| F5 | [MeasureRuleService.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/MeasureRuleService.cs:93>); [ProjectPositionAggregator.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/ProjectPositionAggregator.cs:131>) | [O31: Sanierungsmassnahmen_Kanalisation_Bürglen_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.01_Farb-Grossgrund/GKS/Sanierungsmassnahmen_Kanalisation_Bürglen_5.01.pdf>), PDF-Seite 4; `installation_zwei_haltungen` |
| F6 | [SchachtMeasureFactory.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/SchachtMeasureFactory.cs:30>); [measure_templates.json](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Config/measure_templates.json:187>) | Revision PDF 146–147, 174–175; `schachtliner_menge3`; [SchachtMassnahmenKatalogStore.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Schacht/SchachtMassnahmenKatalogStore.cs:122>) |
| F7 | [measure_templates.json](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Config/measure_templates.json:7>) | [O16: Offerte_26100038-1-0_Sanierung, Seilergasse 17, Altdorf UR.pdf](<D:/Fachwissen/Offerten/Leitungen/Offerte_26100038-1-0_Sanierung, Seilergasse 17, Altdorf UR.pdf>), PDF-Seite 3; [O31: Sanierungsmassnahmen_Kanalisation_Bürglen_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.01_Farb-Grossgrund/GKS/Sanierungsmassnahmen_Kanalisation_Bürglen_5.01.pdf>), PDF-Seite 5; [O20: Offerte_26100245-1-0_Sanierung, Flueelerstrasse, Altdorf UR.pdf](<D:/Fachwissen/Offerten/Offerte_26100245-1-0_Sanierung, Flueelerstrasse, Altdorf UR.pdf>), PDF-Seite 3; Revision PDF 119, 125 |
| F8 | [ProjectPositionAggregator.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Costs/ProjectPositionAggregator.cs:65>); [NpkOfferPdfModelFactory.cs](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Output/Offers/NpkOfferPdfModelFactory.cs:91>) | Revision PDF 33, 39, 74, 81, 103, 105, 108, 111, 119, 128, 131, 134, 147, 175, 250; `liner_dn400` |
| Preise | [cost_catalog.json](<C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Config/cost_catalog.json:1>) | [O29: Kanalsanierung_Bürglen_Zone_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.01_Farb-Grossgrund/Fretz/Kanalsanierung_Bürglen_Zone_5.01.pdf>), PDF-Seite 2–4; [O31: Sanierungsmassnahmen_Kanalisation_Bürglen_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.01_Farb-Grossgrund/GKS/Sanierungsmassnahmen_Kanalisation_Bürglen_5.01.pdf>), PDF-Seite 4–5; [O33: Sanierungsmassnahmen_Kanalisation_5.01.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.01_Farb-Grossgrund/ITS/Sanierungsmassnahmen_Kanalisation_5.01.pdf>), PDF-Seite 6; [O37: Sanierungsmassnahmen_Kanalisation_5.13.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.13_Grossgrund_Galgenwäldli/ITS/Sanierungsmassnahmen_Kanalisation_5.13.pdf>), PDF-Seite 9; [O40: Sanierungsmassnahmen_Kanalisation_5.14.pdf](<D:/Fachwissen/Offertenvergleich/Gep Bürglen/Offerten/Zone_5.14_Grund_Löwenmatt/ITS/Sanierungsmassnahmen_Kanalisation_5.14.pdf>), PDF-Seite 10; [O16: Offerte_26100038-1-0_Sanierung, Seilergasse 17, Altdorf UR.pdf](<D:/Fachwissen/Offerten/Leitungen/Offerte_26100038-1-0_Sanierung, Seilergasse 17, Altdorf UR.pdf>), PDF-Seite 3; [O20: Offerte_26100245-1-0_Sanierung, Flueelerstrasse, Altdorf UR.pdf](<D:/Fachwissen/Offerten/Offerte_26100245-1-0_Sanierung, Flueelerstrasse, Altdorf UR.pdf>), PDF-Seite 3–4 |

Die PDF-Seitenangaben zählen ab der ersten Seite der Datei; aufgedruckte Seitennummern können abweichen.

## Anhang: alle 44 Katalogeinträge

Diese Zuordnung trennt die strenge Revision von der erlaubten Praxisvereinfachung. „Stk“ statt „St“ und „pl“ statt „gl“ sind allein keine Geldfehler.

| Eintrag | D/V27 im Programm | Einheit | Ergebnis |
|---|---|---|---|
| `INSTALL_HL_ANLAGE` | 151.001 | pl | Leistung grundsätzlich passend; globaler Mengenbezug, gl statt pl (F5). |
| `INSTALL_UV_ANLAGE` | 151.001 | pl | Wie HL; dieselbe Revisionsposition beschreibt das Verfahren, nicht automatisch einen zweiten Projektansatz. |
| `INSTALL_MANUELL` | 121.001 | pl | 121 = Einrichtung für Vorarbeiten, nicht allgemeine manuelle Schachtsanierung. |
| `INSTALL_ROBOTER` | 141.001 | pl | 141 = Einrichtung Reparatur Abschnitt 500; für reine Vorarbeiten-Fräsrobotik fachlich prüfen. |
| `VORARBEIT_REINIGUNG` | 211.110 | m | Revisions-Gruppentitel mit Stunden; Meterleistung in AWU-Präzisierung ausdrücklich vereinbart. |
| `VORARBEIT_VD` | – | pro Tag | Bewusst ohne Revisionsnummer; konkrete Verkehrsregelung separat beschreibbar. |
| `VORARBEIT_FRAESEN` | 321.111 | m | 321 bezeichnet Reprofilierung; zudem Exportumrechnung/Reihenfolgefehler F3. |
| `VORARBEIT_EINMESSUNG` | 351.101 | Stk | 351.101 gilt DN 100–200; für grössere DN unzutreffend. |
| `VORARBEIT_ANSCHLUSS_EINMESSEN` | – | Stk | Zuordnung fehlt; Revision 351.201 ff. bietet passende DN-abhängige Positionen. |
| `VORARBEIT_TV_VORKONTROLLE` | 221.102 | m | 221.102 in dieser Revision nicht vorhanden; 221 beschreibt Schiebekamera, h. Praxis m ist separat vereinbart. |
| `VORARBEIT_WASSERHALTUNG` | 411.100 | Stk | 411.100 = Überschrift bis DN 150; Konzept, Anzahl und DN nicht aufgelöst. |
| `SCHLAUCHLINER_NADELFILZ` | 612.111 | m | 612.111 = Lieferung Hauptkanal bis DN 250; Einbauumfang unklar (F7), DN statisch (F8). |
| `SCHLAUCHLINER_NADELFILZ_OPENEND` | 612.112 | m | 612.112 ist gültiger Wiederholungsbereich zu .111; keine normativ fest benannte Open-End-Variante. |
| `SCHLAUCHLINER_GFK` | 612.113 | m | 612.113 ist gültiger Wiederholungsbereich zu .111; keine normativ fest benannte GFK-Variante; bis DN 250. |
| `LINERENDMANSCHETTE_LEM` | 621.110 | Stk | 621.110 Gruppentitel; Unterpositionen je DN. Harte Sperre < 200 falsch (F1). |
| `KURZLINER_PARTLINER` | 521.110 | Stk | 521.110 Gruppentitel DN 100–150; Kurzlinerlänge/Verfahren und Unterposition fehlen. |
| `MANSCHETTE_EDELSTAHL` | 522.110 | Stk | 522.110 Gruppentitel DN 100–125/Länge 9–14 cm; passt nicht allgemein Quick-Lock. |
| `ANSCHLUSS_AUFFRAESEN` | 616.110 | Stk | 616.110 Gruppentitel Hauptkanal DN 150–200; Anschluss-DN und ausführbare Unterposition fehlen. |
| `ANSCHLUSS_EINBINDEN` | 617.110 | Stk | 617.110 Gruppentitel Hauptkanal DN 150–200; Anschluss-DN und Unterposition fehlen. |
| `ANSCHLUSS_DICHTEN` | 515.110 | Stk | 515.110 Gruppentitel Hauptkanal DN 151–200; Unterposition/Verfahren fehlen. |
| `ANSCHLUSS_VERSCHLIESSEN` | 517.110 | Stk | 517.110 Gruppentitel Hauptkanal DN 151–200; Unterposition/Verfahren fehlen. |
| `HAUPTARBEIT_HINDERNISSE_ROBOTER` | 311.111 | h | 311.111 gilt DN 100–200; Stunden passen, DN und Zusammenführung F3 nicht. |
| `QK_DICHTHEITSPRUEFUNG` | – | Stk | Bewusster Verweis auf separates Kapitel 112; hier keine vollständige Unterpositionsprüfung möglich. |
| `QK_TV_ABNAHME` | 221.102 | m | Wie TV-Vorkontrolle; Auftrag Abnahme ist eigenständige Leistung. |
| `QK_DOKUMENTATION` | 234.101 | Stk | 234.101 gilt zusammenfassendem Untersuchungsbericht auf Papier; nicht beliebiger Gesamtdokumentation. |
| `SCHACHT_SANIERUNG_PAUSCHAL` | – | St | Freie Pauschale vertretbar; Startpreis 0, Leistungsumfang je Schacht angeben. |
| `SCHACHT_REINIGUNG` | – | St | Revisionszuordnung fehlt, Startpreis 0; ältere Vergleichsnummer ist keine D/V27-Zuordnung. |
| `SCHACHT_SCHUTZFOLIE` | 711.101 | m2 | 711.101: DN 600 und Länge m, nicht m² (F6). |
| `SCHACHT_LINER_LIEFERN` | 712.101 | m | 712.101 nicht vorhanden; 712.111 ff. sind Stückpositionen nach DN/Höhe (F6). |
| `SCHACHT_LINER_EINBAUEN` | 716.101 | m | 716.101 nicht vorhanden; 716.111 ff. sind Stückpositionen nach DN/Höhe (F6). |
| `SCHACHT_ANSCHLUESSE_AUFFRAESEN` | 721.101 | St | 721.101 nur Anschluss bis DN 100; allgemeine Anwendung auf andere DN falsch. |
| `SCHACHT_LINERENDEN` | 723.101 | St | 723.101 für DN 600; übrige DN und manuelle Übergänge getrennt. |
| `SCHACHT_BANKETT_HANDLAMINAT` | 725.101 | St | 725.101 liegt im Wiederholungsbereich .007–.889 wie .006; DN und Beschreibung müssen ergänzt sein. |
| `SCHACHT_STEIGEISEN_ERSETZEN` | 933.300 | St | 933.300 Gruppentitel; Lieferung/Montage ab .301, Demontage separat zu prüfen. |
| `SCHACHT_RAHMEN_DECKEL` | – | St | Freie Leistung vertretbar; Startpreis 0, Umfang Lieferung/Demontage angeben. |
| `SCHACHT_FUGEN_INJEKTION` | – | St | Freie Leistung; Materialmenge und Arbeitsumfang nicht durch 1 St allein bestimmt. |
| `SCHACHT_REGIE_STD` | – | Std | Freie Stundenleistung; Preis und Besetzung der Gruppe festlegen. |
| `INSTALL_REINIGUNG` | 111.001 | pl | 111.001 Einrichtung grundsätzlich passend; gl statt pl, ohne Preis abgewählt. |
| `INSTALL_ZUSTANDSERFASSUNG` | 112.001 | pl | 112.001 Einrichtung grundsätzlich passend; gl statt pl, ohne Preis abgewählt. |
| `HAUPTARBEIT_REINIGUNG_KANAL` | 211.112 | h | 211.112: h korrekt, aber nur DN 151–800; Default wird auch ausserhalb verwendet. |
| `HAUPTARBEIT_ROTIERDUESE` | 211.312 | h | 211.312 und h passen; tatsächliche Einsatzdauer erforderlich. |
| `HAUPTARBEIT_TV_FAHRWAGEN` | 222.112 | h | 222.112: h korrekt, aber nur DN 151–300; Default wird auch ausserhalb verwendet. |
| `HAUPTARBEIT_FACHARBEITER_TV` | 222.701 | h | 222.701 und h passen; zusätzlicher Mitarbeiter, keine automatische Pflichtmenge. |
| `QK_BERICHT_SPEICHERMEDIUM` | 234.102 | St | 234.102 und St passen für zusammenfassenden Bericht; Abrechnung je Lieferung/Projekt klären. |

## Nachweise und Wiederholung

- [Laufzeitproben](nachweise/probe-ergebnis.json), [synthetisches Excel](nachweise/probe-nullpreis.xlsx), [synthetisches DN-400-LV](nachweise/probe-liner-dn400.csv).
- [Quellprogramm der Proben](nachweise/Program.cs), [Infrastrukturtests](nachweise/npk-infrastructure.trx), [UI-Tests](nachweise/npk-ui.trx), [Quelleninventar](nachweise/quellen-inventar.json), [Quellstand](nachweise/quellstand.json).
- Ausführung der Proben vom Repository-Stamm: `dotnet run --project .tmp/npk-audit/Audit.csproj --no-restore`. Die Probe verwendet vorhandene Release-Bibliotheken und synthetische Haltungen, ohne Anwendungsstart und ohne Kundenprofil. Die archivierte Projektdatei enthält relative Referenzpfade für den ursprünglichen Prüfpfad `.tmp/npk-audit`.
- Gezielt ausgeführt: Infrastrukturfilter `Npk|ProjectPositionAggregator|HoldingMeasureFactory|SchachtMeasureFactory|MeasureRule|MeasurePricing|CostCatalogStore|SchachtLvCostLoader` und UI-Filter `BuilderPageLvPreparation|SanierungsMatrixPrintLvConsistency|NpkExcel`, jeweils `FullyQualifiedName~...`, Release, `--no-build --no-restore`. Kein neuer vollständiger Produktbuild für diesen lesenden Audit.
- Keine Preisänderung, kein Commit, kein Merge oder Push im Rahmen dieses Audits.
