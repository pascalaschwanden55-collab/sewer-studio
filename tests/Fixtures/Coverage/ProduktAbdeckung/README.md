# Beispiel zur Produktabdeckung (von Hand nachrechenbar)

Zwei Berichte wie aus zwei Testprojekten, Pfadwurzel `C:/repo`:

| Datei | infra | ui | zählt? | Ergebnis |
|---|---|---|---|---|
| `src/Lib/A.cs` Zeile 1 | getroffen | – | ja | abgedeckt |
| `src/Lib/A.cs` Zeile 2 | – | getroffen | ja | abgedeckt (ui schreibt `C:\REPO\SRC\…`, dieselbe Zeile) |
| `src/Lib/A.cs` Zeile 3 | – | – | ja | offen |
| `src/Lib/B.cs` Zeilen 10, 11 | | – | ja | offen |
| `tests/Lib.Tests/ATests.cs` | getroffen | | nein, Testcode | |
| `src/Lib/obj/Debug/Gen.g.cs` | | | nein, erzeugt | |
| `src/Lib/Form.Designer.cs` | | getroffen | nein, erzeugt | |

**Produktabdeckung: 2 von 5 Zeilen = 40,00 %.** Teilwert `src/Lib/B.cs`: 0 von 2.

Die alte Verlaufszahl summiert die Kopfwerte (`lines-covered`/`lines-valid`) beider Berichte:
5 von 12 = 41,67 %. Sie zählt Test- und erzeugten Code mit und `A.cs` doppelt.
