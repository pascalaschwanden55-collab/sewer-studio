# Wiederherstellung des KI-Gehirns (C:\KI_BRAIN)

> Kurzanleitung, falls die SSD mit `C:\KI_BRAIN` ausfaellt oder die Wissens-DB
> beschaedigt ist. Der konsistente Spiegel liegt auf `E:\Brain`
> (siehe `Backup_KI_BRAIN.bat` auf dem Desktop).
>
> **Verifiziert am 2026-06-06** mit einem echten Restore-Test (siehe unten):
> Spiegel-DB integrity_check = ok, 21.860 Samples, 21.860 Embeddings,
> 21.860 Embeddings∙Samples-JOIN (= die Basis, die das Retrieval laedt).

## Wann brauche ich das?
- `C:` / die Gehirn-SSD ist defekt oder weg.
- Die App meldet "Lernbasis unzureichend" / "Samples: 0/3", obwohl vorher 21.860 da waren.
- `KnowledgeBase.db` laesst sich nicht mehr oeffnen (Beschaedigung).

## Restore in 4 Schritten

1. **App schliessen** (SewerStudio.exe), damit nichts auf die DB zugreift.

2. **Spiegel zurueckkopieren** – PowerShell:
   ```powershell
   # Ziel-Ordner anlegen, falls C:\KI_BRAIN fehlt
   New-Item -ItemType Directory -Force C:\KI_BRAIN | Out-Null
   # Den kompletten Spiegel zurueckspielen (E: -> C:)
   robocopy E:\Brain C:\KI_BRAIN /MIR /XF _spiegel_log.txt /R:2 /W:3 /MT:8
   ```
   Hinweis: `/MIR` macht `C:\KI_BRAIN` exakt gleich dem Spiegel. Wenn nur die
   Datenbank kaputt ist, reicht auch nur die eine Datei:
   ```powershell
   Copy-Item E:\Brain\KnowledgeBase.db C:\KI_BRAIN\KnowledgeBase.db -Force
   ```

3. **Integritaet pruefen** (sollte `ok` ergeben):
   ```powershell
   python -c "import sqlite3;print(sqlite3.connect(r'C:\KI_BRAIN\KnowledgeBase.db').execute('PRAGMA integrity_check').fetchone()[0])"
   ```

4. **App ueber den Starter oeffnen** (`Start_SewerStudio.bat` auf dem Desktop) –
   er setzt `SEWERSTUDIO_KNOWLEDGE_ROOT=C:\KI_BRAIN`. Im Kopf der App muss
   wieder stehen: **"KI-Modell einsatzbereit | Samples: 21860 | Codes: 207"**.
   Damit ist auch das Retrieval (Few-Shot aus der KB) wieder aktiv.

## Restore-Test (jederzeit gefahrlos wiederholbar)
Prueft, ob der Spiegel im Ernstfall wirklich traegt – **ohne** das echte Gehirn anzufassen:
```powershell
New-Item -ItemType Directory -Force C:\tmp\restore_test | Out-Null
Copy-Item E:\Brain\KnowledgeBase.db C:\tmp\restore_test\KnowledgeBase.db -Force
python -c "import sqlite3;c=sqlite3.connect(r'C:\tmp\restore_test\KnowledgeBase.db');print('integrity:',c.execute('PRAGMA integrity_check').fetchone()[0]);print('Samples:',c.execute('SELECT COUNT(*) FROM Samples').fetchone()[0]);print('Embeddings:',c.execute('SELECT COUNT(*) FROM Embeddings').fetchone()[0])"
```
Erwartung: `integrity: ok`, `Samples` und `Embeddings` jeweils > 0 (aktuell je 21.860).

## Gut zu wissen
- Der Spiegel laeuft automatisch beim Schliessen der App (Starter-Fenster offen lassen)
  oder per Doppelklick auf `Backup_KI_BRAIN.bat`. `E:` muss verbunden sein.
- Vor jedem Spiegel laeuft ein SQLite-WAL-Checkpoint, damit die kopierte DB
  konsistent (in EINER Datei) ist – deshalb ist die Kopie restore-faehig.
- Kein Enterprise-RPO/RTO – bewusst schlank gehalten fuer den Einzelplatz-Betrieb.
