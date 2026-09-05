# Qwen3.8 (27B) als Bildmodell — Messung vom 2026-09-04

**Frage:** Hilft das lokal installierte `qwen3.8` (27,3 Mrd. Parameter, Q4_K_M, 17 GB, Vision +
Denkmodus, Ollama 0.33.3) im Codiermodus mehr als das heutige `qwen3-vl:8b-q8`?

**Antwort:** Als „Ist da etwas?“-Erkenner ja, als Codierer nein. Qwen3.8 sieht auf 16 von 23
Schadensbildern einen Befund (8B: 1 von 23), trifft aber den VSA-Hauptcode nur 0 bis 2 Mal und
meldet auf 4 von 9 sauberen Bildern einen Fehlalarm. Keine Umstellung, keine Freigabe.

## Messgrundlage

- Werkzeug: `tools/EvalSetBenchmark --review-file C:\KI_BRAIN\eval_review\v1_event_metadata_review.json`
  (reiner Ollama-Bildlauf ohne Sidecar-Hinweise, ohne QualityGate).
- Bilder: die 32 persoenlich geprueften Bilder der Schadensreview (23 mit Schaden, 9 ohne;
  Review vollstaendig, 0 Konflikte, Candidates-SHA `7c385453…`).
- Beide Modelle mit identischem Prompt (`EnhancedVisionPromptBuilder`, ohne Katalog), identischem
  JSON-Schema und `OllamaDeterministicOptions` (temperature 0, seed 42, num_ctx 12288).
- Ergebnisdateien (gitignored): `docs/benchmarks/eval_20260904_123305_qwen3_vl_8b_q8_reviewed_damage.*`
  und `docs/benchmarks/eval_20260904_123714_qwen3_8_reviewed_damage.*`.

## Ergebnis des Werkzeugs

| 32 geprueft Bilder | qwen3-vl:8b-q8 (heute) | qwen3.8 |
|---|---|---|
| Schaden gefunden | 1 / 23 | 7 / 23 |
| Fehlalarm | 0 / 9 | 2 / 9 |
| Code exakt | 0 / 23 | 0 / 23 |
| Hauptcode richtig | 0 / 23 | 2 / 23 |
| Ohne verwertbaren Code | 0 | 12 |
| Zeit je Bild (warm) | 0,8 s | 3 bis 18 s |

Die 12 „ohne verwertbaren Code“ sind KEINE Modellfehler: Das Werkzeug wertet nur `vsa_code_hint`.
Qwen3.8 beschreibt den Befund oft im Klartext („Ablagerungen an der Sohle“, „Wurzeleinwuchs“)
und laesst das im Schema erlaubte Nullfeld leer. Solche Bilder zaehlen als unaufgeloest.

## Diagnose-Nachlaeufe (Python, exakt dieselbe Anfrage, Rohantworten gespeichert)

| Variante | Befund gefunden | Fehlalarm | Hauptcode richtig | ohne Code | Zeit/Bild |
|---|---|---|---|---|---|
| A: Denken an (heutiges Verhalten) | 16 / 23 | 4 / 9 | 0 / 23 (1 mit Stichwort-Zuordnung) | 13 / 32 | 9,0 s |
| B: `think:false`, Modell-Standard-Sampling | 18 / 23 | 6 / 9 | 0 / 23 | 14 / 32 | 4,7 s |
| C: Denken an, `vsa_code_hint` Pflicht aus 20 Katalogcodes | 16 / 23 | 4 / 9 | 1 / 23 (exakt 0) | 0 / 32 | 8,8 s |

Lauf C beantwortet die Kernfrage: Auch mit Codezwang codiert Qwen3.8 falsch. 11 von 32 Bildern
werden `BBC` (Ablagerung); die 7 `BAF`-Bilder (Oberflaechenschaden) werden 5x BBC, 2x BABBA;
die 5 `BAIZ`-Bilder (einragendes Dichtungsmaterial) werden BAB, BAJ, BAA oder leer. Der Befund
passt zur Diagnose vom 2026-07-21: Das Modell kennt die VSA-Taxonomie nicht, nicht die Bilder.

Zwischen Werkzeuglauf und Nachlauf A schwanken die Zahlen um etwa 2 Bilder trotz temperature 0
und seed 42 (Denkmodus plus GPU). 32 Bilder sind eine Standortbestimmung, keine Statistik.

## Drei Fallen fuer den Fall, dass Qwen3.8 je eingesetzt wird

1. **`think:false` mit temperature 0 dreht durch.** Auf einem Testbild lief das Modell 3 von 3
   Mal bis zum Kontextende (7696 Token, 44 s) und lieferte kaputtes JSON. Mit dem
   Modell-Standard-Sampling (temperature 1, top_p 0,95) antwortet es in 3 bis 5 s sauber, aber
   inhaltlich schlechter (Labels wie „ABC“, „Abbildung“). Der Denkmodus (Ollama-Standard, da
   SewerStudio kein `think`-Feld sendet) liefert die brauchbarsten Antworten bei ~9 s je Bild.
2. **Grafikspeicher.** Qwen3.8 belegt geladen rund 19 bis 21 GB. Die Sidecar-Reserve
   (`SEWER_SIDECAR_VRAM_RESERVE_GB`, Standard 12) ist auf das 8B abgestimmt; mit Qwen3.8 im
   Speicher verweigert der Sidecar DINO (4 + 12 = 16 GB benoetigt) mit `insufficient_vram`.
   8B + YOLO + DINO + SAM = ~23 GB passen; 27B + Sidecar-Stapel = ~33 GB passen nicht in 32,6 GB.
   Der faire Pruefer-Test (`--full-chain`: DINO, SAM, dann Qwen mit deren Treffern als Kontext)
   braucht deshalb zuerst eine Ladereihenfolge oder Entladestrategie.
3. **Schema-Nullfeld.** Solange `vsa_code_hint` null sein darf, misst der Review-Benchmark ein
   beschreibendes Modell systematisch zu tief. Fuer einen Modellvergleich Codezwang erwaegen,
   ohne den produktiven Vertrag zu aendern.

## Was daraus folgt

- Kein Wechsel der Automatik (`GpuModelSelector.LargeModel` bleibt `qwen3-vl:8b-q8`).
- Qwen3.8 als Codierer bringt heute nichts; als Praesenz-Erkenner ist es dem 8B weit voraus,
  aber mit fast der Haelfte Fehlalarmen (meist „Ablagerung in der Sohle“).
- Der Hebel bleibt derselbe wie am 2026-07-21 und 2026-07-10: Taxonomie ueber Kontext
  (DINO/SAM-Treffer, Katalog, bestaetigte Beispiele) statt groesseres Modell.
