# Laufende Nummer in den Aufklapplisten

Seit 09.09.2026 zeigen Haltungen und Schächte links die Spalte „Nr.“.
Sie liest die vorhandene laufende Projektnummer. Beim Filtern bleibt diese
Nummer erhalten; eine Neunummerierung wird sofort angezeigt.

Bei Schächten wird das Nummernfeld mit der vorhandenen Regel
`SchaechteFieldLogic.ResolveNrColumnName` erkannt. Damit funktionieren auch
die bisherigen Schreibweisen `NR`, `NR.` und `Nr.`. Die Schachtbezeichnung
steht weiterhin getrennt daneben. Die Anzeige schreibt keine Daten.

Prüfung: beide `AufklappListeIsolatedSmokeTests` prüfen die echte WPF-Anzeige,
Filterung und Wertänderungen. `SchachtLaufnummerConverterTests` prüft die
Schreibweisen und fehlende Nummern.
