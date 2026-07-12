# PC-Ausfallschutz – kurzer manueller Test

1. In den Einstellungen **PC-Ausfallschutz** starten und einen leeren USB-Ordner waehlen.
2. Nach dem Lauf pruefen, ob der Status `kopiert` und `vollstaendig geprueft` meldet.
3. Eine kleine Quellcodedatei aendern und die Sicherung nochmals starten.
4. Im zweiten Lauf muss nur die geaenderte Datei neu kopiert werden.
5. Die vorherige Fassung muss unter `SewerStudio_Datensicherung\_Versionen` liegen.

Zusaetzliche Schutzpruefung: Ein fast volles Testlaufwerk muss vor dem Kopieren mit
`Zu wenig freier Speicherplatz` abgelehnt werden. Die KI-Wissensdatenbank muss sich
aus der gesicherten `KnowledgeBase.db` oeffnen lassen, auch wenn SewerStudio beim
Sicherungslauf weiterlief.
