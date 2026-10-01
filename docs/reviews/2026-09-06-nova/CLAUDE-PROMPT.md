# Überarbeitungsprompt für Claude

Überarbeite meine Datei „SewerStudio-Nova-Komplett.html“. Nutze „SewerStudio-Nova-Vorschau.html“ als gestalterische Referenz. Erstelle eine neue Datei „SewerStudio-Nova-Optimiert.html“. Bewahre die Originale.

Ziel ist eine ruhige, gut lesbare und zuverlässig bedienbare Arbeitsoberfläche für Kanalinspektionen. Die Gestaltung soll professionell wirken und lange Arbeitssitzungen erleichtern. Die Bewertung „9 von 10“ ist ein Qualitätsziel; behaupte sie erst nach nachvollziehbarer Prüfung.

## 1. Gute Gestaltung erhalten

Behalte die erkennbare Nova-Gestaltung, die deutsche Navigation, Hell/Dunkel/System und die fachlichen Zustandsfarben Z0–Z4. Erhalte alle 15 Seiten und die drei Arbeitsfenster. Erstelle eine kurze Liste ihrer Funktionen, damit beim Umbau nichts verschwindet. Ordne selten benötigte Aktionen unter „Weitere Aktionen“ ein; entferne keine Fachfunktion stillschweigend.

Reduziere Glas, Leuchten und Bewegung dort, wo Daten gelesen oder bearbeitet werden. Nutze einheitliche Abstände und eine klare Hauptaktion pro Arbeitsbereich. Modellnamen und Rechnerauslastung gehören in aufklappbare Details. Zeige im Alltag die nächste fachliche Aufgabe.

## 2. Bestätigte Fehler beheben

- Bei Wechsel von 78998-79002 zu 07.6588-6587 ändern sich derzeit nur Überschriften. Alle Werte, Formulare und Medien müssen dem ausgewählten Datensatz folgen. Nutze einen zentralen Beispieldatenbestand mit stabilen Objektkennungen. Zeige ungespeicherte Änderungen; ermögliche Speichern, Verwerfen und Abbrechen des Wechsels. Beschreibe ehrlich, ob Speichern nur innerhalb der Demo gilt.
- Die Feldsuche „Baujahr“ bei Haltungen versteckt auch Schachtfelder. Begrenze Suche, „Alle auf/zu“ und Ansichtswechsel auf ihre Seite beziehungsweise Schaltflächengruppe. Schachtsuche muss eigenständig arbeiten.
- Die Übersicht zeigt 12 dringende Haltungen, aber Z0 = 8 und Z1 = 15 ergeben 23. Auch 168 / 239 ist 70,29 %, nicht 71 %. Berechne alle Zähler und Diagramme aus denselben Daten. Benenne den Unterschied zwischen automatisch analysiert und fachlich geprüft.
- „Nächste Haltung prüfen“ muss tatsächlich eine offene Haltung auswählen und den passenden Arbeitsbereich öffnen.
- Player → Ereignis erfassen → Übernehmen muss zum Player zurückführen. Haltung, Videoposition und Auswahl bleiben erhalten. Abbrechen übernimmt nichts. Dasselbe gilt für Codierung aus dem Training Studio.
- „Ruhig“ muss auch die Canvas-Hintergrundanimation stoppen. Speichere die Wahl und berücksichtige reduzierte Bewegung des Betriebssystems. Unnötige dekorative Schleifen in verdeckten Ansichten pausieren.

## 3. Arbeitsfläche neu aufteilen

Prüfe mindestens 1366 × 768, 1440 × 900 und 1920 × 1080. Im Standardzustand sollen bei 1366 × 768 mindestens sechs Haltungen sichtbar bleiben. Das ist ein Abnahmeziel; löse Platzprobleme durch einklappbare Details und verschiebbare Trennlinien. Verkleinere dafür keine Schrift unter die vereinbarte Grenze.

Halte Werkzeugleisten kompakt. Lege die Grösse von Tabellen, Formularen und Video anhand der verfügbaren Fläche fest. Das Video darf keine Nachbarspalte aus dem Fenster drücken. Wiedergabetasten, Zeitposition und wichtige Prüfaktionen bleiben sichtbar. Im Training Studio muss die komplette Codierungs- und Freigabespalte erreichbar sein. Speichere sinnvolle persönliche Einstellungen für Spalten und Aufteilung.

## 4. Lesbarkeit und Tastatur

Mindestens 11 für normale Programmtexte; nutze für häufige Beschriftungen bevorzugt 12–13 und für Daten 13–15. Prüfe Hell und Dunkel auf ausreichenden Kontrast: normaler Text mindestens 4,5:1 als Ziel. Mache Zustand, Fehler, Auswahl, gesperrte Felder und ungespeicherte Änderungen auch ohne Farbe verständlich.

Benutze echte beschriftete Schaltflächen, Eingabefelder und Auswahlfelder. Navigation muss mit Tab und Enter funktionieren. Globale Suche und angezeigte Tastenkürzel müssen tatsächlich reagieren. Dialoge erhalten zugängliche Namen und passende Rollen, sichtbaren Fokus und eine korrekte Rückkehr zur vorherigen Aufgabe. Esc schliesst die oberste Dialogebene. Alle Symbole brauchen eine verständliche Bezeichnung.

Referenzen: https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html und https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/.

## 5. Ehrlicher und vollständig nutzbarer Prototyp

Liefere vollständiges HTML mit DOCTYPE, UTF-8, lang="de" und viewport. Verwende lokale oder vorhandene Systemschriften. Die Datei muss offline per Doppelklick funktionieren.

Implementiere die Kernabläufe mit klar gekennzeichneten Beispieldaten. Eine reine Vorschau darf keinen echten Import, Datenbankzugriff, Export oder Modelltest vortäuschen. Noch nicht umgesetzte Aktionen sind erkennbar und haben eine kurze Erklärung. Zeige sinnvolle Leer-, Lade-, Fehler- und Erfolgszustände. Für KI-Prozentwerte müssen Bedeutung und Grenzen erkennbar sein. Trenne KI-Vorschlag, fachliche Bestätigung und Freigabe für Training.

Schreibgeschützte Kundenoriginale sind eine feste Projektregel. Ein Export muss Ziel und Ergebnis verständlich nennen. Unterscheide eine revidierte XTF-Datei von einem echten Rückabgleich mit GEONIS.

## 6. Nachweise mitliefern

Liefere die neue vollständige HTML-Datei, eine kurze Änderungsliste und eine Prüftabelle. Prüfe ausdrücklich: Datensatzwechsel, Speichern/Verwerfen/Abbrechen, unabhängige Suchen, korrekte Summen, Rückkehr aus der Codierung, ruhiger Modus und Tastaturbedienung. Öffne alle 15 Seiten und drei Fenster. Kennzeichne jeden Prüfpunkt als bestanden, fehlgeschlagen oder nicht geprüft. Zeige Bildschirmbilder der Übersicht, Haltungen und des Training Studios in den genannten Grössen. Behaupte keine Prüfung, die du nicht ausgeführt hast.

Der spätere Einbau in SewerStudio ist ein eigener Schritt. SewerStudio bleibt eine WPF-Anwendung. Ordne dann Gestaltung und Verhalten den vorhandenen Schrift-, Farb- und FluentIcon-Bausteinen zu. Die bestehende Schriftskala lautet 11, 12, 13, 15, 18, 22 und 28. Prüfe Windows-Skalierung 125 % und 150 % in der echten Anwendung. Ein Browserbild ersetzt diese Prüfung nicht. Keine neuen Pakete, keine grossen Klassen erweitern und keine gespeicherten Datenformate nebenbei ändern.
