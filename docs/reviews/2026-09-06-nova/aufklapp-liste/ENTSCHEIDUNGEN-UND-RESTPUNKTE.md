# SDD ledger — plan: docs/superpowers/plans/2026-09-08-nova-haltungen-aufklapp-liste.md
Branch feature/nova-haltungsliste ab cfccda464. Spec: Pascals Wunsch 08.09. + Nova-Komplett.html (Desktop) Themen-Aufklapper.
Pre-flight scan:
| Paar | Schnittstelle | Befund |
| T1/T2 | HaltungAufklappListe (Aufgeklappt, DetailBuilder, Themen, VideoCommand/ProtokollCommand, KlappeAuf/Zu) + DataPageAufklappListeController | Signaturen im Plan fixiert |
| T1 | HaltungThemenGruppierung aus HaltungFelderDrawer extrahiert, Drawer verwendet sie weiter | Waechter per Grep |
| T2 | HaltungenAnsichtRegel WPF-frei; Sichtbarkeit Liste/Tabelle/Alt | Nova-Layout-Schalter bleibt |
| T2 | Abdocken in Listenansicht deaktiviert | Ruling: Fachfunktion bleibt ueber Tabelle |
Rulings: genau EINE Haltung offen (Akkordeon), Auswahl per Pfeiltaste klappt nicht auf; Standard "liste" fuer alle; Spaltenchips in der Liste ausgeblendet.
Task 1: BASE fa6a9e343, dispatched (opus)
Plan um Tasks 4-6 (Haltungsgrafik, Schachtgrafik, Abnahme) ergaenzt (Pascal 08.09.); Ruling: Haltungsgrafik ersetzt Rohrring, Schachtgrafik ersetzt Grundriss-Kreis
Plan: Task 6 Schaechte-Aufklapp-Liste (Pascal 08.09.), Abnahme = Task 7
Task 1: Umsetzer 73de30275 (Agent aa2744d9ad0e01775)
Task 1: review Spec ok (3 vertretbare Abweichungen: Button statt ToggleButton; Controller hoert nicht selbst auf Selected — Task 2 ruft AktualisiereFormular; 4 statt 5 RecordDetailsView bei zugeklapptem Thema), Qualitaet Needs fixes (1 schwer Escape aus Editor, 4 gering). minor (deferred): Binding Source=Laenge fuer Literal; Ampel 9 als Zahl.
Task 1: fix round 1/5 gestartet
Task 1: fix round 1 done 858055a67 (Tastenregel, TwoWay-Themenzustand, CacheLength 1). Offen: Virtualisierung mit offener Zeile im fensterlosen Messaufbau nicht beweisbar (Messung am laufenden Programm nach Task 2/3).
Task 1: Re-Review Offen (Escape-Test still gruen: UpdateSource vor Escape; verschwundener Datensatz ungetestet). fix round 2/5 gestartet (echtes Fenster fuer Fokus).
Task 1: fix round 2 done 5cead8c59 (HaltungAufklappListeFokusTests mit echtem Fenster, kein UpdateSource von Hand; verschwundener Datensatz getestet). Controller-Sichtpruefung + eigener Lauf 32 gruen.
Task 1: complete (73de30275, 858055a67, 5cead8c59)
Task 2: BASE 5cead8c59, dispatched (opus)
Task 1 Grenze: Fokustest laeuft ohne echten RecordDetailsView (StaticResource HeaderBrush beim Erzeugen); Zusammenspiel mit dessen LostKeyboardFocus-Handler = Sichtprobe.
Task 2: Umsetzer 237edf5ab (Agent a7b345173fc74cf09)
Task 2: Umsetzer 237edf5ab + fe9f5231d. Review: Spec 1 Abweichung (Konfliktrueckruf erreicht Liste nicht), Qualitaet Needs fixes (2 Major: Konflikthinweis unsichtbar, Loeschen liest Grid-Auswahl; 2 Mittel: doppelte Formulare, ScrollIntoView synchron; 3 Klein). minor (deferred): Elemente-Record mit 10 positionalen Parametern; leere Liste ohne Leerzustand; ColumnViewChipSync nur Haltungen; Ansichtswechsel schreibt ShowHaltungenNovaLayout nicht (Bestand).
Task 2: fix round 1/5 gestartet
Task 2: fix round 1 done 1e0d3e338 (Konfliktweiche, MarkierteZeilen, ein Formular, DataPageDockingHost ausgelagert — Abdocken ungetestet: Sichtprobe). Re-Review: Offen (HaltungAnzeigen nach Loaded nicht wieder abonniert; WendeAn doppelt; Rueckweg-Test fehlt). fix round 2/5 gestartet.
Task 2: fix round 2 done bc5942218; Controller-Sichtpruefung + eigener Lauf 305 gruen. Grenze: Seitentest mit echtem VM schreibt settings.json (Persistenz nicht abschaltbar).
Task 2: complete (237edf5ab, fe9f5231d, 1e0d3e338, bc5942218)
Ruling: Task 3 auf "Bilder ueber Pruefhost" verkleinert und parallel zu Task 4 (nur docs/werkzeug); Abnahme, Release-Lauf, CLAUDE.md in Task 7.
Task 4: BASE bc5942218, dispatched (opus). Task 3 (Bilder): dispatched (sonnet, parallel, nur docs)
Bilder Liste (Controller-Sicht): Kopfzeilen einzeilig, Themen 17/9/11/3 + Weitere zu, Felder editierbar, Uebersicht gefuellt. Fuer die Fixwelle: Kopf "PROTOK..." abgeschnitten (Spalte breiter/kuerzerer Titel), Badge "fachlich geprueft" im dunklen Theme kontrastarm (SuccessSubtle-Tinte).
Task 3 (Bilder): a849ad5db, 22 Kopfzeilen zu / 4 RecordDetailsView auf, Befund PROTOK... ; Rest der Abnahme in Task 7
Task 4: Umsetzer 210df3098, 4d0b758e2 (Agent a08d7dbf98cb03ece). Controller-Sicht im Pruefhost: Grafik im Panel winzig (ganzes A4-SVG samt Labeltabelle in 300x250 gequetscht, unlesbar). Ruling: im Panel nur die Rohrsaeule (Rohr, Skala, Symbole, Knoten, Fliesspfeil) ohne Labeltabelle, Hoehe mind. 320 px / ~55 % der Panelhoehe, Tooltips je Symbol. Fix-Runde parallel zum Review gestartet.
Task 4: Review Spec ok (2 begruendete Abweichungen: Protocol.Current statt ResolveEntriesForExport — richtig, rein lesend; Hoehe fest 700), Qualitaet Needs fixes (Absturzluecke XmlException/FormatException; Waechter "rein lesend" ohne VsaFindings; Leerzustand ungetestet; Flag ohne Sync; SvgWert schluckt Tokens; Attribut-Vertrag nur ueber Beispiel). minor (deferred): Notfarben Colors.Gray/SystemColors; Farbwaechter nur #RRGGBB; Marken rechnet Labels zweimal; Loaded abonniert nicht neu (wie Panel); SymbolAnzahl-DP Testoberflaeche. Alle in die laufende Fix-Runde gegeben.
Task 4: fix round 1 vom Umsetzer a08d7dbf98cb03ece bei Sitzungslimit abgebrochen (uncommittet, baut); Fortsetzung durch Agent a7060a6b1c418261a (sonnet) mit task-4-fix-brief.md
Task 4: fix round 1 done 2168f8842 (Fortsetzung)
Task 4: Controller-Sicht Bild grafik2: Rohrsaeule gross, Skala, Knoten, Symbole — gut. Eigener Lauf 283 UI / 62 Pipeline gruen. Re-Review laeuft; Task 5 parallel gestartet (neue Dateien, kein Ueberschneidungsrisiko ausser SvgTeilmengeZeichner).
Task 5: BASE 2168f8842, dispatched (sonnet)
Task 4: Re-Review Behoben. minor (deferred): kein Hash-Test fuer PDF-Unveraenderlichkeit; Hoehe fest 360 statt 55 %. Hinweis: SewerStudio.exe laeuft im Hauptbaum (Merge-Build spaeter beachten).
Task 4: complete (210df3098, 4d0b758e2, 2168f8842)
Task 5: Umsetzer 72a4c9066, 9f00a59e4 (Agent a5983cfff2e381e66)
Task 5: Controller-Sichtpruefung: Grafik zu klein/Beschriftungen unlesbar, Zu-/Ablauf ueberlappt -> Fix-Runde 1 (gleiches Ruling wie Task 4: volle Breite, >=320 px, Striche >=3 px, Schrift >=9 px nach Skalierung, schmales hohes SVG, hoechstens zwei beschriftete Stummel je Seite).
Task 6: BASE 9f00a59e4, dispatched (sonnet) — Schaechte als Aufklapp-Liste
Task 5: Review Spec ok (Richtung Zulauf/Ablauf fachlich gegen XtfNeuPlanBuilder geprueft), Qualitaet Needs fixes (Symbolkategorie bei PDF-Schachtprotokollen immer generisch; SchachtgrafikAnsichtBuilder ohne Test) -> in die laufende Fix-Runde gegeben. minor (deferred): Schluessel Ort vs vsa.schachtbereich; Anschluss teilt die Wandzone.
Task 5: fix round 1 done c46547110
Task 5: Controller-Sicht nach Fix: Grafik lesbar (Zonen, Tiefe 2.0, 1100x900, Symbole je Zone, Zulauf/Ablauf-Pfeil). Restbefunde fuer die Abnahme: Zulaufbeschriftung "10001-1..." klebt am Rand, Fliesspfeil sehr gross, Zustandsmarke Z0 im Schachtkopf doppelt (Kopfzeile + Panel).
Task 5: Re-Review Offen (hoch: Kategorie-Textregel ohne Wortgrenzen/Negation und fuer jeden Eintrag; mittel: c46547110 enthaelt fremden Task-6-Code, isoliert nicht baubar) -> Fix-Runde 2. Lehre: zwei Agenten im selben Worktree = Commits vermischen sich; kuenftig sequenziell oder getrennte Dateien.
Task 6: Umsetzer 4083137d9 (Agent af2e2a317b44c277d)
Task 6: DONE 4083137d9 (Agent af2e2a317b44c277d). Eigener Lauf 286 gruen. Bedenken: zweites Control SchachtAufklappListe statt Generik (geteilte WPF-freie Logik), kein Konflikthinweis auf der Schachtseite (Bestandsluecke, kein Regress), Pruefhost-Variante klappt nur Haltungen auf.
Task 5: fix round 2 done 0c78c2a06 (Wortgrenzen, Negation, Bauteilbindung).
Task 6: Review Spec 1 Abweichung + Qualitaet Needs fixes (hoch: zwei Live-Syncs desselben Schachts in der Listenansicht, LeereFelderDrawer fehlt; niedrig: tote SetzeSichtbar-Ueberladung) -> Fix-Runde 1. minor (deferred): SchaechtePage.xaml.cs 988/1000; PfeilBeschriftung zwei Quellen; kein Konflikthinweis auf der Schachtseite (Bestand).

Task 6: fix round 1 done ed3268616 (LeereFelderDrawer fuer Schaechte, tote Ueberladung weg, Konvertertext eine Quelle). Uebergabe an Codex geschrieben: docs/reviews/2026-09-06-nova/aufklapp-liste/UEBERGABE-CODEX.md
