# Excel-Export: alles auf einem Tabellenblatt

Korrigierter Stand vom 08.09.2026 nach ausdrücklichem Nutzerwunsch.

- Genau ein Tabellenblatt je Haltungs- bzw. Schachtexport.
- Diagramme und Gesamtkennzahlen oben, vollständige Datenliste darunter.
- Alle 27 Haltungs- bzw. 17 Schachtspalten bleiben unverändert erhalten.
- Keine getrennte Übersicht und keine zusätzliche Textdetails-Tabelle.
- Filter-Summen für Anzahl, Länge und Kosten bleiben auf demselben Blatt.
- Kennungen bleiben beim seitlichen Scrollen sichtbar.
- Lange Texte bleiben vollständig in der Originalzelle. In Excel ist der
  volle Inhalt über die Bearbeitungsleiste zugänglich.
- Druck: alle Spalten auf einer A3-Seitenbreite. Die Anzahl Seiten untereinander
  richtet sich nach der Anzahl und Höhe der Datenzeilen.

Die frühere Aufteilung in mehrere Blätter ist damit aufgehoben.
Die Originaldatei im Downloads-Ordner bleibt unverändert.

Prüfung: 139 Excel-Tests bestanden, Build ohne Fehler und Warnungen.
Nachweis: `.tmp/excel-bedienung/tests/excel-ein-blatt.trx`.
Synthetische Beispiele über den echten Exportdienst erzeugt und mit LibreOffice
als Druckbild geprüft. Keine direkte Bedienprobe in Microsoft Excel.
