# Player: Tastatursteuerung – 03.10.2026

## Abgegrenztes Paket

Ausgangspunkt ist `master 72d47f298`, getrennt von dem noch offenen Kontextpaket
PR 84. `PlayerKeyboardPresenter` übernimmt Tastaturfokus, Hilfe und den Anschluss
an bestehende Player-Aktionen. Drei Fensterhandler reichen nur noch Ereignisse weiter.
Zwei verwaiste private Controllerfassaden sind entfernt. Die fachlichen Codiertasten
Escape/Erkennung/Markierwerkzeug sowie Schliessen, Dropdown-Zeitregel und Mausereignisse
bleiben erhalten. Kein Canvas-Umbau und keine spätere Hauptarbeitswelle übernommen.

Zwei Sol-Agenten mit mittlerem Aufwand hatten getrennte Schreibzuständigkeiten für
Produktion und Tests. Ein Sol-Agent mit hohem Aufwand hat den eingefrorenen
Code-/Teststand unabhängig geprüft und freigegeben.
Root integriert und führt Builds/Tests zentral aus. Kundenoriginale, Datensicherung
und älterer Hauptarbeitsstand bleiben erhalten.

## Verhalten zuerst geschützt

1. Aufbau liest keine Quelle und führt keine Aktion aus.
2. Textfokus sperrt Schriftzeichen und Escape vor der Hilfe.
3. F1 öffnet/schliesst Hilfe auch bei Textfokus, ohne Aktionsbesitzer zu erzeugen.
4. Sichtbare Hilfe sperrt Wiedergabe; Escape schliesst zunächst nur die Hilfe.
5. Unbekannte Taste liest aktuelle Aktionen; der Owner behält seine erste Bindung.
6. Escape liest Abbruchfähigkeit nach den Aktionen; `Handled` folgt erst der Aktion.
7. Geworfene Wiedergabeaktion wird als dieselbe Ausnahme weitergegeben; Taste bleibt unbehandelt.
8. Öffnen/Schliessen markiert das Ereignis vor der Sichtbarkeitsänderung.
9. Ohne Test-Fokusport bleibt der ursprüngliche Standardwächter aktiv.

Die zusätzliche Fehlerprüfung stammt aus der unabhängigen Vorprüfung. Alle neun
Erwartungen waren mit einer ausdrücklich bezeichneten Testhilfe des ursprünglichen
Ablaufs grün. Der Originalanschlusswächter bindet sie an die echten Fensterquellen.
Die Erwartungen wurden unverändert auf den echten Presenter umgestellt; die Testkopie
ist vollständig entfernt. Vorhandene Tasten-, Fokus- und Verbotsprüfungen bleiben erhalten.

## Ausgangsprüfungen

- Vollständiger Release-Build: null Warnungen und Fehler.
- Fokus vor zusätzlicher Fehlerprüfung: 85 bestanden, null übersprungen/Fehler.
- Vollständiger Ausgangsfokus mit Fehlerprüfung: 86 bestanden, null übersprungen/Fehler.
- Produktionsdateien waren bis zur grünen Ausgangsprüfung unverändert; SHA-256-Abgleich durchgeführt.
- Endstand: acht Quell-/Testdateien für zentrale Abschlussprüfungen eingefroren.
- Endfokus einschließlich Wartbarkeitswächtern: 94 bestanden, null übersprungen/Fehler.
- Endfokus einschließlich zusätzlichem Wiedergabewächter: 95 bestanden, null übersprungen/Fehler.
- Endstand-Release-Build: null Warnungen und Fehler.
- Unabhängige Code-/Testprüfung einschließlich zusätzlichem Fehlerfall: freigegeben.
- Architekturkarte mit dem echten Anschluss abgeglichen und erfolgreich validiert.

Der erste volle UI-Lauf hatte genau einen Fehler: Ein bestehender Wiedergabewächter
suchte den Steuerhost noch im Tastaturpartial. Die Aktionsbindungen liegen jetzt im
Fensteraufbau. Der Wächter prüft diesen Anschluss, Stop/Pause und die Weitergabe über
Presenter/Owner ausdrücklich. Direkte Playerzugriffe bleiben in Tastaturpartial und
Presenter verboten; Slider-/Knopf-/Controllerprüfungen bleiben erhalten.
Produktions- und bisherige Testdateien blieben unverändert. Nach dieser zusätzlichen
Testanpassung wurden Build, betroffener Fokus sowie ganze UI-/Modernizer-Suiten erneut
erfolgreich geprüft. Die grünen Infrastruktur-/Pipeline-Läufe gelten für unveränderte Quellen.
Alle acht eingefrorenen Quell-/Testdateien sind nach den Läufen unverändert.
Die abschließende Git-Diffprüfung fand bei der neuen Testdatei überzählige
Leerzeilen am Dateiende. Nur diese zwei wurden entfernt; der Inhalt bis zur letzten
schließenden Klammer blieb bytegleich. Die Dateiprüfsumme wurde nachgeführt.
Die unabhängige Prüfung hat diese reine Bereinigung nochmals bestätigt.

| Vollständige Release-Prüfung | Bestanden | Übersprungen | Fehler |
| --- | ---: | ---: | ---: |
| Infrastruktur | 8.256 | 6 | 0 |
| Pipeline | 3.048 | 3 | 0 |
| Oberfläche | 7.916 | 49 | 0 |
| ProjectModernizer | 62 | 0 | 0 |
| Gesamt | 19.282 | 58 | 0 |

Der vollständige Release-Build hatte null Warnungen und Fehler. Die gezielte Prüfung
hatte 95 bestandene Tests ohne Überspringen. Push-Prüfung und GitHub-Status werden
in den getrennten Liefernachweisen festgehalten; diese Prüfung ist kein Programmstart.

## Wartbarkeitsmessung

Alle Dateien unter `UI/Views/Windows`, die `partial class PlayerWindow` erklären,
zählen zusammen. Leer-/Kommentarzeilen zählen mit; eigenständige Helfer und generierter
Code nicht. Der neue Presenter ist eine eigenständige Klasse mit 68 Zeilen.

| Stand | Ganze Fensterklasse | Teildateien | Hauptdatei |
| --- | ---: | ---: | ---: |
| master als Ausgangspunkt | 4.246 | 73 | 568 |
| Tastaturpaket | 4.215 | 73 | 582 |

Die Klasse verliert insgesamt 31 Zeilen; die Hauptdatei enthält zusätzlich die
ausdrücklichen neun Aktionsbindungen. Die Klassengrenze im Wartbarkeitswächter sinkt
auf 4.215. Das Ziel unter 1.000 bleibt offen. PR 84 hat seine eigene Basis und Messung;
bei gemeinsamer Integration sind Messung und Größenwächter erneut abzugleichen.

## Grenzen

Die Verhaltenstests nutzen die echten vorhandenen Controller mit kontrollierten
Aktionen und WPF-Elementen auf einem STA-Thread. Sie starten keinen echten Player,
kein Video und keine KI-Modelle. Architekturtests schützen den tatsächlichen
Fensteranschluss und die Reihenfolge. Keine neue öffentliche Schnittstelle,
Registrierung, Laufzeit, UI/Ai-Klasse, Paket- oder Datenformatänderung.
