# Prüfwerkzeuge erneut verwenden

Beide Werkzeuge nutzen die aktuellen Release-Dateien unter src/AuswertungPro.Next.UI/bin/Release/net10.0-windows10.0.19041. Vorher den aktuellen Programmstand bauen. Sie schreiben ausschließlich in ihre fest zugeordneten Unterordner von .tmp/redesign-audit-20260908.

Hauptprobe: dotnet build werkzeug/Pruefhost.csproj -c Release
Tabellenprobe: dotnet build werkzeug-tabellen/Pruefhost.csproj -c Release

Aufruf der erzeugten Pruefhost.exe (bin/Release/net10.0-windows10.0.19041):
- Hauptprobe: Light Probe – R1/R2/R4/R5/R6
- Hauptprobe: Ressourcen – echte Medienkonflikt-Ressourcen sowie R7/R8
- Hauptprobe: Light TrainingStudio <absoluter PNG-Pfad> 1920 1080 – Stufenmessung
- Hauptprobe: Light Suite – Seitenserie mit bekannten Prüfwerkzeug-Grenzen
- Tabellenprobe: Light Haltungen <absoluter PNG-Pfad> 1920 1080 standard
- Tabellenprobe: Dark Schaechte <absoluter PNG-Pfad> 1920 1080 standard
- Weitere Tabellenvarianten: alle oder ohneauswahl, jeweils bei Haltungen

Die beiden Projekte liegen absichtlich in getrennten Ordnern. Der App-Startup bleibt unterdrückt. Native Videoflächen und Windows-Mica werden von WPF-Bildaufnahmen nicht vollständig erfasst. Ein Exitcode 0 allein beweist nicht jede Gegenprobe; JSON-Nachweise und Prüfwerkzeug-Meldungen lesen.
