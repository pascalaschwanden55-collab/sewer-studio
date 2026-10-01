# Nova-Abgleich des Lieferungs-Editors

Stand: 12.09.2026. Die bestehenden Objektmasken, Kataloge und Nova-Ressourcen sind
weiterhin die Grundlage. Angepasst wurden der neue Lieferungs-Editor und sein
Einstieg im bestehenden Export-Seitenkopf.

Verwendet werden die vorhandenen Bausteine `NovaPageHeader`, `Card`, `ToolbarButton`
und `ToolbarButtonAccent`. Die Themen Hell und Dunkel liefern Farben, Schriftstufen
und Eingabefelder. Sämtliche Daten-/Befehlsbindungen und Normlisten bleiben erhalten.
Die bestehenden Objektmasken, Fachlogik und Datenformate wurden nicht geändert.

- Dev-Build bestanden, keine Fehler; eine bestehende Warnung in `VsaFotoAblageTests`.
- 22 gezielte UI-/Seitenkopf-/Exporttests bestanden. Der WPF-Kindtest wird über seinen Elterntest ausgeführt.
- Echte Fensterdarstellung mit synthetischen Daten in Hell/Dunkel bei 1240 und 900 Pixel Fensterbreite angesehen.
- Der Themenwechsel erhält Eingaben und Auswahl; Speichern und Schliessschutz funktionieren weiter.
- Keine neue globale Palette, kein zusätzlicher Dienst und kein neuer Dropdown-Katalog.

[Hell](lieferung-editor.png) · [Dunkel](lieferung-editor-nova-dunkel.png) ·
[Hell, schmal](lieferung-editor-nova-schmal.png) · [Dunkel, schmal](lieferung-editor-nova-dunkel-schmal.png)

Der vollständige fachliche WebGIS-Nachbau bleibt der im
[Umsetzungsstand](UMSETZUNGSSTAND.md) beschriebene offene Umfang.
