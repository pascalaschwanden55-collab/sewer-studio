# Fortschrittsanzeige der Datensicherung bleibt auf 0 — Plan

**Befund (Pascal, 2026-08-10):** Die Datensicherung laeuft und kopiert, der
Fortschrittsbalken bleibt aber bei 0 %.

**Nicht betroffen sind die Daten.** Am selben Tag geprueft: 4504 Goldbilder,
1826 Dateien des neuen Lernbestands, Golddatei und Wissensdatenbank liegen
vollstaendig auf dem Datentraeger `Elements` (E:). Es ist ein Anzeigefehler.

## Warum dieser Plan nicht mit einer Reparatur anfaengt

Die ganze Kette wurde gelesen und ist korrekt gebaut:

| Stelle | Datei | Zustand |
|---|---|---|
| Bindung | `SettingsPage.xaml:926` | `Value="{Binding FullBackupOperation.Percent}"`, Min 0 / Max 100 — richtig |
| Anzeigezustand | `FullBackupOperationState.cs:17` | `[ObservableProperty] double _percent` — meldet Aenderungen |
| Verdrahtung | `SettingsFullBackupWorkflow.cs:65` | `InlineProgress` reicht an `UpdateProgress` weiter — richtig |
| Berechnung | `SettingsFullBackupPresentationBuilder.cs:44` | `100 * BytesDone / BytesTotal`, 0 wenn `BytesTotal <= 0` |
| Meldung | `FullBackupService.cs:705` | `ProgressState.Report` — wird pro Datei gerufen |
| Rueckruf | `DirectoryMirror.cs:280/307/397/408` | `onFileDone` wird tatsaechlich aufgerufen |

Es ist aus dem Code **nicht** erkennbar, welche der drei moeglichen Ursachen
vorliegt. Eine Reparatur auf Verdacht waere raten. Am 2026-08-10 sind vier
solche Vermutungen hintereinander gebrochen, jede plausibel, jede falsch.

## Die drei moeglichen Ursachen

| # | Ursache | erkennbar am Statustext unter dem Balken |
|---|---|---|
| A | Groessenermittlung findet nichts (`BytesTotal = 0`) | „… 47 von **0** Dateien" |
| B | Ermittlung stimmt, Prozentrechnung/Bindung falsch | „… 47 von **12'843** Dateien", Zahl laeuft hoch, Balken steht |
| C | Meldung kommt nicht durch | Text bleibt vollstaendig stehen |

Der Statustext enthaelt bereits `"{Component}: {FilesDone} von {FilesTotal} Dateien"`
(`SettingsFullBackupPresentationBuilder.cs:56`). Ein Blick auf den Bildschirm
waehrend eines Laufs unterscheidet alle drei — ohne eine Zeile Code.

## AP-1 — Der Test, der die Ursache findet UND sie kuenftig verhindert

**Zuerst**, weil er alle drei Ursachen abdeckt und danach als Schutz stehen bleibt.

**Datei:** `tests/AuswertungPro.Next.Infrastructure.Tests/Backup/FullBackupProgressTests.cs` (neu)

Ein Test, der eine echte Sicherung ueber einen Testordner mit ein paar Dateien
faehrt und den gemeldeten Fortschritt einsammelt. Zusicherungen:

1. Es wird **ueberhaupt** gemeldet (mindestens ein `FullBackupProgress`) — faengt C.
2. `BytesTotal > 0` und `FilesTotal > 0` in jeder Meldung — faengt A.
3. Der letzte gemeldete Prozentwert liegt **ueber 0** — faengt B.
4. `BytesDone` waechst monoton und ueberschreitet `BytesTotal` nie.

Der Test benutzt den echten `FullBackupService` mit einer Testquelle, keinen
Nachbau. Ein Nachbau haette den Fehler nicht, sonst waere er nicht da.

**Messung:** Der Test muss vor der Reparatur **rot** sein. Ist er gruen, liegt
die Ursache nicht im Dienst, sondern in der Oberflaeche — dann weiter mit AP-2b.

## AP-2 — Die Reparatur, je nach Befund

**Erst nach AP-1 entscheiden.** Genau eine der drei greift.

### 2a — falls `BytesTotal = 0` (Ursache A)

`FullBackupService.RunAsync:139` ruft `Analyze(sources, progress: null, ct)`.
Zu pruefen: Warum liefert das 0, obwohl derselbe Aufruf kurz zuvor im
Bestaetigungsdialog eine plausible Groesse zeigt (`SettingsFullBackupWorkflow.cs:50`)?
Verdacht: unterschiedliche `AppSettings` zwischen den beiden Aufrufen, oder eine
Quelle, die zwischen Dialog und Lauf verschwindet.

Zusaetzlich fail-closed absichern: Ist `BytesTotal` 0, obwohl Dateien kopiert
werden, gehoert das als sichtbarer Hinweis in den Statustext — nicht als
stille 0.

### 2b — falls die Zahlen stimmen, der Balken aber steht (Ursache B)

Dann liegt es an der Oberflaeche. Zu pruefen in dieser Reihenfolge:
`ProgressBar.Value` ist `BindsTwoWayByDefault` (RangeBase); eine
Rueckschreibung koennte den Wert ueberschreiben. Abhilfe: `Mode=OneWay`
ausdruecklich setzen.

### 2c — falls gar nichts gemeldet wird (Ursache C)

Dann bricht die Kette zwischen `DirectoryMirror` und `InlineProgress`. Der
Rueckruf ist verdrahtet (`FullBackupService.cs:174`), also zuerst pruefen, ob
der Drosselzweig (`ProgressState.Report:701`) dauerhaft zurueckkehrt.

## AP-3 — Die Anzeige ehrlich machen

Unabhaengig von der Ursache: Ein Balken, der bei 0 steht, sieht aus wie
„haengt". Bei einer Sicherung ueber mehrere Minuten ist das die schlechteste
aller Rueckmeldungen.

**Wenn die Gesamtgroesse nicht bekannt ist, gehoert kein Prozentbalken hin,
sondern ein unbestimmter Balken** (`IsIndeterminate="True"`) plus die laufende
Dateizahl. Das sagt die Wahrheit: „laeuft, Dauer unbekannt" statt „0 %".

`SettingsFullBackupProgressPresentation` bekommt dafuer ein Feld
`GesamtgroesseBekannt`; die Oberflaeche schaltet den Balkenmodus daran.

**Messung:** Ein Test, der eine Meldung mit `BytesTotal = 0` durchreicht und
verlangt, dass die Anzeige als unbestimmt gekennzeichnet ist.

## Reihenfolge

1. **AP-1** — Test schreiben, laufen lassen. Er nennt die Ursache.
2. **AP-2a/b/c** — genau den einen Zweig reparieren, den AP-1 aufzeigt.
3. **AP-3** — die unbestimmte Anzeige, damit ein kuenftiger Fall dieser Art
   nicht wieder wie ein Absturz aussieht.

AP-3 lohnt sich auch dann, wenn AP-2 die Ursache beseitigt: Es gibt Quellen,
deren Groesse sich nicht im Voraus bestimmen laesst.

## Was dieser Plan nicht anfasst

Die Sicherung selbst. Sie arbeitet nachweislich korrekt; jede Aenderung an
`DirectoryMirror` oder am Sicherungsablauf waere Risiko ohne Anlass.
