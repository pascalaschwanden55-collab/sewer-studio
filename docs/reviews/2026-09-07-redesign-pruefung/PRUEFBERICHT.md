# Prüfung des gesamten Nova-Redesigns

**Ergebnis: weitgehend umgesetzt, aber noch nicht vollständig abnahmebereit. Sechs Fehler sind bestätigt.**

Geprüft am 07.09.2026. Ausgangspunkt: `8054e9ebd`, zusammengeführte Nova-Etappen 1 und 2. Die bereits vorhandenen, noch nicht eingecheckten XTF-Arbeiten blieben unverändert. Untersucht wurde der aktuelle Arbeitsstand. Dieser Bericht enthält keine Reparaturen am Produktcode.

## Bestätigte Befunde

### R1 · Hoch · Die globale Suche kann einen Datensatz aus dem vorherigen Projekt öffnen

**Ablauf:** In Projekt A nach einer Haltung suchen. Das Suchfeld stehen lassen und Projekt B öffnen. Anschliessend den bestehenden Treffer mit Enter auswählen.

**Ergebnis:** Die Haltungsseite von B erhält den Datensatz aus A als Auswahl. Die Gegenprobe bestätigt `altTrefferNochVerwendet=true` und `auswahlIstImAktivenProjekt=false`. Damit gehört das angezeigte beziehungsweise bearbeitete Objekt nicht zum aktiven Projekt. Ein tatsächlicher Verlust gespeicherter Kundendaten wurde nicht erzeugt oder behauptet.

**Ursache:** Die Treffer werden ausschliesslich bei einer Änderung des Suchtexts erneuert. Die Suche beobachtet keinen Projektwechsel. Bei der Auswahl wird die Zugehörigkeit zum aktuellen Projekt nicht geprüft.

**Code:** [GlobaleSucheViewModel.cs:21](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/GlobaleSucheViewModel.cs:21), [Auswahl:35](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/GlobaleSucheViewModel.cs:35).

**Korrektur:** Bei Projektwechsel Treffer, Markierung und Suchfenster zurücksetzen beziehungsweise neu berechnen. Vor jeder Navigation die Projektzugehörigkeit prüfen. Verhaltenstest mit zwei unterschiedlichen Projekten ergänzen.

### R2 · Hoch · Nach einem Projektwechsel zeigt die Übersicht weiterhin das alte Projekt

**Ablauf:** Auf der neuen Übersicht unter „Letzte Projekte“ ein anderes Projekt öffnen.

**Ergebnis:** Die Shell wechselt erfolgreich zu Projekt C mit null Haltungen. Die sichtbare Übersicht bleibt dieselbe Instanz und zeigt weiter „Projekt B“ mit einer Haltung. Titel, Kennzahlen und die zugrunde liegende Projektzuordnung widersprechen sich. Die Gegenprobe benutzt den echten `ProjektOeffnenCommand` mit einer gespeicherten künstlichen Projektdatei.

**Ursache:** Das neue Übersichtsmodell beobachtet nur die beim Erstellen vorhandene Haltungsliste. Es meldet sich nicht auf einen Wechsel von `Shell.Project` an. Sein Öffnen-Befehl lädt das Projekt, erneuert die Seite aber nicht. Die klassische Übersicht besitzt einen entsprechenden Projektwechsel-Abonnenten.

**Code:** [ProjektUebersichtPageViewModel.cs:56](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/Pages/ProjektUebersichtPageViewModel.cs:56), [Projekt öffnen:235](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/Pages/ProjektUebersichtPageViewModel.cs:235).

**Korrektur:** Projektwechsel abonnieren, alte Sammlung abmelden, neue Sammlung anmelden und alle angezeigten Werte erneuern. Ein Test muss den Wechsel über die sichtbare Projektaktion abdecken.

### R3 · Hoch · Im Training Studio sind die Schadensstufen 4 und 5 abgeschnitten

**Ablauf:** Training Studio öffnen, Bereich „2 · Fachliche Codierung“ betrachten.

**Ergebnis:** Die Knöpfe 4 und 5 liegen ausserhalb des sichtbaren Bereichs. Das passiert bereits bei **1920 × 1080 und 100 % Windows-Skalierung**. Die zusätzlich geprüfte Fenstergrösse 1280 × 720 zeigt denselben Fehler.

**Messung:** Alle fünf Knöpfe setzen `Width=40`, erben aber `MinWidth=100` und werden tatsächlich 100 Pixel breit. Die Reihe benötigt mindestens 516 Pixel; die rechte Spalte hat insgesamt 330 Pixel. Bei Full HD beginnt Knopf 5 bei x=1991, ausserhalb des 1920 Pixel breiten Fensters. Auch Knopf 4 liegt ausserhalb der sichtbaren Inhaltsfläche. Vertikales Scrollen behebt das nicht.

**Code:** [TrainingStudioWindow.xaml:505](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml:505), [Spaltenbreite:265](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml:265), [geerbte Mindestbreite](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml:257).

**Nachweis:** [Full-HD-Bild](bilder/Light-TrainingStudio-1920x1080.png), [gemessene Knopfbreiten](nachweise/messung-TrainingStudio-Light.json).

**Korrektur:** Für diese Reihe eine passende Mindestbreite oder fünf gleich breite Spalten vorgeben. Ein echter Layouttest muss alle fünf Knöpfe innerhalb der sichtbaren Fläche nachweisen. Die konkurrierenden Breiten stammen teilweise aus dem Bestand; das übernommene Redesign behebt sie nicht.

### R4 · Mittel · KI-Durchläufe werden zwischen Projekten vermischt

**Ablauf:** Einen Durchlauf in Projekt A merken und anschliessend Projekt B öffnen.

**Ergebnis:** Die Projektübersicht von B zeigt weiter den Lauf „nur-in-Projekt-A“. Das Sitzungsregister unterscheidet ausschliesslich Haltungsnamen. Gleichnamige Haltungen verschiedener Projekte überschreiben daher denselben Registereintrag. Der Knopf „Prüfen“ sucht den Namen anschliessend im aktuell geöffneten Projekt. Bei einer Namensgleichheit führt er damit zu einem anderen Projektobjekt als dem Ursprung des angezeigten Laufs; ohne Namensgleichheit tut er nichts.

**Code:** [CodingSuggestionRegistry.cs:14](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionRegistry.cs:14), [Anzeige aller Läufe:101](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/Pages/ProjektUebersichtPageViewModel.cs:101), [Navigation:223](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ViewModels/Pages/ProjektUebersichtPageViewModel.cs:223).

**Korrektur:** Projektkennung und stabile Haltungskennung im Register mitführen. Die Projektübersicht auf das aktive Projekt begrenzen. Alternativ das Register beim Projektwechsel kontrolliert zurücksetzen und verspätete Ergebnisse alter Läufe abweisen.

### R5 · Mittel · Die globale Suche verdrängt Schacht- und Strassentreffer

**Ablauf:** Nach einer Strasse mit mindestens zwölf passenden Haltungen suchen.

**Ergebnis:** Bei 13 passenden Haltungen und sechs Schächten liefert die Gegenprobe zwölf Haltungen, keinen Schacht und keinen Strassentreffer. Der direkte Sprung zur ganzen Strasse ist dadurch bei grösseren Beständen nicht erreichbar. Auch eine gesuchte Schachtnummer kann durch passende Haltungsnamen verdrängt werden.

**Ursache:** Zuerst werden sämtliche Haltungen angefügt, danach Schächte und zuletzt Strassen. Erst danach wird die gemeinsame Liste auf zwölf Einträge gekürzt. Es gibt weder Vorrang für exakte Treffer noch reservierte Plätze für die drei Trefferarten.

**Code:** [GlobaleSucheRegel.cs:29](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/UseCases/Suche/GlobaleSucheRegel.cs:29), [Begrenzung:47](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/UseCases/Suche/GlobaleSucheRegel.cs:47).

**Korrektur:** Exakte Nummern und passende Strassen priorisieren oder die zwölf Plätze auf Treffergruppen verteilen. Einen Test mit mehr als zwölf passenden Haltungen ergänzen.

### R6 · Mittel · Ein rechteckiger Schacht wird als Oval gezeichnet

**Ablauf:** Einen Schacht mit der Form „Rechteckig“ auswählen.

**Ergebnis:** Die WPF-Gegenprobe bestätigt: `Oval=Visible`, `Rechteck=Collapsed`. Die Anzeige widerspricht damit der erfassten Bauwerksform. Auch unbekannte Formen fallen auf einen Kreis zurück; das beweist keine runde Bauwerksform.

**Code:** [SchachtUebersichtPanel.xaml.cs:124](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Views/Pages/Schachtansicht/SchachtUebersichtPanel.xaml.cs:124).

**Korrektur:** Rechteckig auf eine rechteckige Zeichnung abbilden. Unbekannte Formen sichtbar als unbekannt behandeln. Einen Verhaltenstest je unterstützter Form ergänzen.

Die gemeinsamen Gegenproben R1, R2, R4, R5 und R6 stehen in [verhaltensprobe.json](nachweise/verhaltensprobe.json).

## Was geprüft wurde

Grundlage waren der freigegebene Nova-Prototyp v2, das Etappe-2-Inventar, die bisherige Abnahme sowie der echte Code und die Tests. Bewusst genehmigte Abweichungen wurden nicht als Fehler gewertet: bestehende Zustandsfarben, dunkler Flächenakzent, Zustandslegende statt Donut und Trainingsfreigabe über das bestehende Exportregister.

| Bereich | Prüfung und Ergebnis |
|---|---|
| Gemeinsames Design | Beide Themes, Ressourcen, Schriften, Farben, Kontrastregeln, Knopfvorlagen und Seitenköpfe über Code und bestehende Tests geprüft. |
| Navigation und Kopfzeile | Gruppen, Suche, Aufgabenhinweis, Speicheranzeige und KI-Bereitschaft untersucht. R1 und R5 bestätigt. |
| Projektübersicht | Kennzahlen, Kostenanbindung, Zustands-/Schadensfilter, Projektwechsel und Sitzungsregister untersucht. R2 und R4 bestätigt. |
| Haltungen | Neue Arbeitsfläche, Spaltenansichten, Eingabefelder, Live-Abgleich, Rohrring und Platzregel geprüft. Die vorhandenen Layout- und Konflikttests bestehen. |
| Schächte | Spaltenansichten, Eingabefelder, Übersicht und Formen geprüft. R6 bestätigt. |
| Player | Geänderter Kopf, Bedienleiste, Menüs, Tastenkürzel-Verdrahtung und Codiermodus-Anbindung gelesen; zusätzliche Fensteraufnahme mit künstlichem Video. |
| Training Studio | Drei Spalten, Quellenleiste, Codierung, Freigabeweg und Beschriftungen gelesen; neue Aufnahmen bei Full HD und kleinerer Fensterfläche. R3 bestätigt. |
| Weitere Seiten | Projekt, Import, Export, Druckcenter, Dossiers, beide Sanierungsmatrizen, Schattenauswertung, VSA, Diagnose und Einstellungen mit künstlichem Projekt in beiden Themes aufgebaut. |
| Medienkonflikte | Separat mit den echten kompilierten App-Ressourcen aufgebaut. Erfolgreich; siehe Prüfwerkzeug-Grenze unten. |
| Aufbau des Codes | Neue Fachregeln liegen überwiegend in Application. Die bestätigten Lücken betreffen vor allem Projektbindung, Registerzuordnung und echtes Laufzeitlayout. |

Es entstanden 36 WPF-Aufnahmen. Die Bildserie ersetzt keinen vollständig durchgeklickten Import-, Export- oder Codierablauf. Sie enthält künstliche Daten und keine Kundenoriginale.

## Aktuell ausgeführte Prüfungen

| Prüfung | Ergebnis |
|---|---|
| `dotnet build AuswertungPro.Dev.slnf -c Release --no-restore` | Bestanden, 0 Fehler, 0 Warnungen. Erster Versuch scheiterte am Zugriff auf eine Build-Zwischendatei; Wiederholung mit normalem Dateizugriff bestand. |
| Vollständiges UI-Testprojekt, Release | **6’522 bestanden, 10 übersprungen, 0 fehlgeschlagen.** Die zehn Einträge sind Kindprozess-Szenarien, deren übergeordnete Tests bestanden. |
| Gezielte neue Application-Regeln im Infrastructure-Testprojekt | **61 bestanden, 0 übersprungen, 0 fehlgeschlagen.** Suche, nächste Aufgabe, Kennzahlen, Rohrgeometrie, Fakten, Schadensgruppen, Register und Vorschau-PDF. |
| Zusätzliche eigene Gegenproben | Fünf Funktionsfehler bestätigt; zusätzlich Training-Layout mit tatsächlichen Elementmassen geprüft. |
| `dotnet build AuswertungPro.sln -c Release --no-restore` | **Nicht bestanden:** Das laufende `SewerStudio.McpServer.dll` lässt sich nicht ersetzen. MSB3027/MSB3021 beim Kopieren. Die betroffenen laufenden Prozesse wurden nicht beendet. |

Die grünen Bestandstests decken die sechs gefundenen Fälle noch nicht ausreichend ab. Das ist eine konkrete Testlücke, kein Beweis für fehlerfreies Verhalten.

Build- und Testausgaben liegen in `nachweise/`. Die maschinenlesbare Zusammenfassung enthält den geprüften Commit und die aktuellen Testzähler. Sidecar-/QGIS-Tests und die übrigen vollständigen Testprojekte wurden in dieser Prüfung nicht erneut ausgeführt.

## Grenzen und verworfene Verdachtsfälle

- Gemessen wurde Windows-DPI 96, also 100 %. Die zusätzliche Fensterprobe 1280 × 720 nähert den verfügbaren Platz bei höherer Skalierung an, ist aber **kein echter 125-/150-%-DPI-Test**. Diese Windows-Skalierungen bleiben offen.
- Der Prüfhost unterdrückt den produktiven Programmstart und verwendet eigene Projekt-, Profil- und Wissensordner. Die echten Sicherungs-, Import-, QGIS- und KI-Startabläufe wurden damit nicht abgenommen.
- Die Bilder stammen aus WPF-Rendering. Das Videobild von LibVLC bleibt darin schwarz, weil es in einem getrennten nativen Fenster gezeichnet wird. Daraus wurde kein Playerfehler abgeleitet.
- Der erweiterte Etappe-2-Prüfhost meldet bei Medienkonflikten eine fehlende Ressource `SecondaryButton`. Die gezielte Gegenprobe mit `App.InitializeComponent()` und den echten kompilierten Ressourcen besteht. Dieser **Prüfhost-Effekt wird nicht als Produktfehler geführt**. In `seitenprobe.json` bleiben seine beiden fehlgeschlagenen Bildversuche transparent erhalten.
- Früh aufgenommene Zwischenbilder mit noch laufender Einblendung oder falscher Ersatzfarbe der Mica-Fläche wurden erneuert. Die abgelegten Serienbilder warten die Einblendung ab. Aus diesen Zwischenständen wurden keine Kontrastfehler abgeleitet.
- Der fachliche Prüfstatus wird weiterhin aus „offen/abgeschlossen“ und offenen KI-Protokolleinträgen abgeleitet. Seine Eignung als Nachweis einer persönlichen Fachprüfung wurde nicht anhand echter Projekte abgenommen. Das ist eine bekannte fachliche Prüfgrenze, kein zusätzlich bestätigter Fehler dieses Berichts.
- Die laufenden XTF-Änderungen erscheinen im aktuellen Export-/Schachtseitenstand. Dieser Bericht beurteilt sie nicht als Bestandteil des Redesigns.

## Nachweise erneut ausführen

Das Werkzeug liegt unter `werkzeug/`. Es verwendet die Release-Dateien dieses Arbeitsordners und schreibt ausschliesslich nach `.tmp/redesign-review/isoliert`. Der synthetische Videoclip ist für den Modus `Player` nötig, nicht für die Fehlergegenproben.

```powershell
dotnet build werkzeug/Pruefhost.csproj -c Release
# Aus dem Berichtordner heraus; die Programmdatei liegt anschliessend unter:
# werkzeug/bin/Release/net10.0-windows10.0.19041/Pruefhost.exe
# Argumente: Light Probe                 -> R1/R2/R4/R5/R6
#            Ressourcen                  -> echte Medienkonflikt-Ressourcen
#            Light Suite                 -> Seiten und Themes
#            Light TrainingStudio <PNG> 1920 1080 -> Stufenknöpfe samt Messung
```

**Reihenfolge für die Nachbesserung:** zuerst R1 und R2 zum Schutz der Projektzuordnung sowie R3 für die vollständige Stufenauswahl. Danach R4 bis R6. Anschliessend die sechs Gegenproben in dauerhafte Verhaltenstests überführen und echte Bedienproben bei 125 % und 150 % ergänzen.
