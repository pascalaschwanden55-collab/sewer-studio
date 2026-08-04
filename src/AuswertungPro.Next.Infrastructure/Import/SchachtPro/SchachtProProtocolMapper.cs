using System.Globalization;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>
/// Mappt ein SchachtPro-Protokoll (ProtocolDto) auf Schacht-Felder und strukturierte
/// Protokoll-Eintraege (Schaden = Bauteil-Code + Beschreibung mit Label und Norm-Info).
/// Bauteil-Namen und Reihenfolge folgen der bestehenden Ordnung
/// <see cref="SchachtProtocolParser.SchachtComponentOrder"/> des PDF-Imports,
/// damit UI und Export beide Importwege gleich darstellen.
/// </summary>
internal static class SchachtProProtocolMapper
{
    internal sealed record MappedProtocol(
        IReadOnlyList<(string Field, string Value)> Fields,
        List<ProtocolEntry> Entries,
        IReadOnlyList<string> UnknownLabels);

    /// <summary>Sektion -> Zustands-Map des DTO (Namen wie in der App / SchachtComponentOrder).</summary>
    private static IReadOnlyList<(string Section, Func<ProtocolDto, Dictionary<string, bool>?> Map)> SectionAccessors
        => new (string, Func<ProtocolDto, Dictionary<string, bool>?>)[]
        {
            ("Schacht", d => d.SchachtZustand),
            ("Schachtdeckel", d => d.DeckelZustand),
            ("Deckelrahmen", d => d.DeckelrahmenZustand),
            ("Schachthals", d => d.SchachthalsZustand),
            ("Konus", d => d.KonusZustand),
            ("Schachtrohr", d => d.SchachtrohrZustand),
            ("Bankett", d => d.BankettZustand),
            ("Durchlaufrinne", d => d.DurchlaufrinneZustand),
            (SchachtProZustandMapper.SectionSteighilfe, d => d.LeiterSteigeisen),
            (SchachtProZustandMapper.SectionTauchbogen, d => d.Tauchbogen)
        };

    /// <summary>
    /// Mappt Stammdaten, Zustände und Anschluesse. Bei LITE-Projekten nur
    /// Schachtnummer, Datum, Bemerkung, GPS (Fotos behandelt der Service).
    /// </summary>
    internal static MappedProtocol Map(ProtocolDto dto, bool isLite)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var fields = new List<(string Field, string Value)>();
        var entries = new List<ProtocolEntry>();
        var unknown = new List<string>();

        void Add(string field, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                fields.Add((field, value.Trim()));
        }

        if (isLite)
        {
            Add(SchachtProFieldNames.AusfuehrungDatumJahr, dto.Datum);
            Add(SchachtProFieldNames.AusfuehrungDatumJahrAscii, dto.Datum);
            Add(SchachtProFieldNames.DatumJahr, dto.Datum);
            Add(SchachtProFieldNames.Bemerkungen, dto.Bemerkungen);
            AddGps(fields, dto);
            return new MappedProtocol(fields, entries, unknown);
        }

        // --- Stammdaten (geteilte kanonische Namen) ---
        Add(SchachtProFieldNames.AusfuehrungDatumJahr, dto.Datum);
        Add(SchachtProFieldNames.AusfuehrungDatumJahrAscii, dto.Datum);
        Add(SchachtProFieldNames.DatumJahr, dto.Datum);
        Add(SchachtProFieldNames.Wetter, dto.Wetter);
        Add(SchachtProFieldNames.Funktion, dto.SchachtFunktion);
        Add(SchachtProFieldNames.Schachtform, dto.Schachtform);
        Add(SchachtProFieldNames.Medium, dto.Medium);
        Add(SchachtProFieldNames.Material, dto.MaterialSchacht);
        Add(SchachtProFieldNames.Schachttiefe, dto.Tiefe);
        Add(SchachtProFieldNames.Bemerkungen, dto.Bemerkungen);
        Add(SchachtProFieldNames.Schachtlaenge, dto.Laenge);
        Add(SchachtProFieldNames.Schachtbreite, dto.Breite);

        var dimension = dto.Dimension;
        if (string.IsNullOrWhiteSpace(dimension)
            && !string.IsNullOrWhiteSpace(dto.Laenge)
            && !string.IsNullOrWhiteSpace(dto.Breite))
        {
            dimension = $"{dto.Laenge.Trim()} x {dto.Breite.Trim()}";
        }

        Add(SchachtProFieldNames.Dimension, dimension);
        Add(SchachtProFieldNames.Durchmesser, dimension);

        if (dto.Doppelschacht)
            Add(SchachtProFieldNames.Doppelschacht, "Ja");

        // --- Schachtaufbau ---
        Add(SchachtProFieldNames.RahmenDeckelHoehe, dto.RahmenDeckelHoehe);
        Add(SchachtProFieldNames.Deckelmaterial, dto.DeckelMaterial);
        Add(SchachtProFieldNames.Deckelform, dto.Deckelform);
        Add(SchachtProFieldNames.Deckeltyp, dto.DeckelTyp);
        Add(SchachtProFieldNames.Belastungsklasse, dto.Belastungsklasse);
        Add(SchachtProFieldNames.Deckeldurchmesser, dto.DeckelDurchmesser);
        Add(SchachtProFieldNames.SchachthalsForm, dto.SchachthalsForm);
        Add(SchachtProFieldNames.SchachthalsDimension, dto.SchachthalsDimension);
        // App-Kommentar ProtocolEntity: schachthalsZwischenKonusHoehe = Hoehe des Schachthals (Hals),
        // schachthalsHoehe = Hoehe des Teils unter dem Konus (Oberteil).
        Add(SchachtProFieldNames.SchachthalsHoehe, dto.SchachthalsZwischenKonusHoehe);
        Add(SchachtProFieldNames.SchachthalsDurchmesser, dto.SchachthalsDurchmesser);
        Add(SchachtProFieldNames.SchachthalsZwischenKonusDurchmesser, dto.SchachthalsZwischenKonusDurchmesser);
        if (dto.Konus)
        {
            Add(SchachtProFieldNames.KonusVorhanden, "Ja");
            if (dto.KonusExzentrisch)
                Add(SchachtProFieldNames.KonusExzentrisch, "Ja");
        }

        Add(SchachtProFieldNames.KonusHoehe, dto.KonusHoehe);
        Add(SchachtProFieldNames.KonusForm, dto.KonusForm);
        Add(SchachtProFieldNames.KonusDimension, dto.KonusDimension);
        Add(SchachtProFieldNames.KonusDurchmesserOben, dto.KonusDurchmesserOben);
        Add(SchachtProFieldNames.KonusDurchmesserUnten, dto.KonusDurchmesserUnten);
        Add(SchachtProFieldNames.SchachtoberteilForm, dto.SchachtOberteilForm);
        Add(SchachtProFieldNames.SchachtoberteilHoehe, dto.SchachthalsHoehe);
        Add(SchachtProFieldNames.SchachtoberteilDimension, dto.SchachtOberteilDimension);
        Add(SchachtProFieldNames.SchachtunterteilForm, dto.SchachtUnterteilForm);
        Add(SchachtProFieldNames.SchachtrohrHoehe, dto.SchachtrohrHoehe);
        Add(SchachtProFieldNames.SchachtunterteilDimension, dto.SchachtUnterteilDimension);
        Add(SchachtProFieldNames.SkizzenNotiz, dto.SkizzeNotiz);

        AddGps(fields, dto);

        // --- Steighilfe-Inventar (Radio leiter/steigeisen) ---
        if (dto.LeiterSteigeisen is not null)
        {
            if (dto.LeiterSteigeisen.TryGetValue("leiter", out var leiter) && leiter)
                Add(SchachtProFieldNames.Steighilfe, "Leiter");
            else if (dto.LeiterSteigeisen.TryGetValue("steigeisen", out var steigeisen) && steigeisen)
                Add(SchachtProFieldNames.Steighilfe, "Steigeisen");
        }

        // --- Zustände -> Protokoll-Eintraege ---
        var anyMaengelfrei = false;
        foreach (var (section, accessor) in SectionAccessors)
        {
            var map = accessor(dto);
            if (map is null || map.Count == 0)
                continue;

            var sectionEntries = MapSection(section, map, unknown, ref anyMaengelfrei);
            entries.AddRange(sectionEntries);
        }

        // --- Anschluesse ---
        if (dto.Anschluesse is { Count: > 0 } anschluesse)
        {
            Add(SchachtProFieldNames.Anschluesse, FormatAnschluesse(anschluesse));

            foreach (var anschluss in anschluesse)
            {
                if (anschluss.Zustand is null || anschluss.Zustand.Count == 0)
                    continue;

                var prefix = $"Nr. {anschluss.Nr}: ";
                var anschlussEntries = MapSection(
                    SchachtProZustandMapper.SectionAnschluss,
                    anschluss.Zustand,
                    unknown,
                    ref anyMaengelfrei,
                    prefix);
                entries.AddRange(anschlussEntries);
            }
        }

        // --- Zustands-Bemerkungen an den ersten Eintrag der Sektion haengen ---
        if (dto.ZustandBemerkungen is { Count: > 0 } bemerkungen)
        {
            foreach (var (section, text) in bemerkungen)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var target = entries.FirstOrDefault(e =>
                    string.Equals(e.Code, section, StringComparison.Ordinal));
                if (target is not null)
                    target.Beschreibung += $" (Bemerkung: {text.Trim()})";
            }
        }

        // --- Primaere Schaeden (Zusammenfassung wie beim PDF-Import) ---
        if (entries.Count > 0)
        {
            var summary = string.Join("; ", entries
                .GroupBy(e => e.Code, StringComparer.Ordinal)
                .OrderBy(g => SchachtProtocolParser.GetComponentOrderIndex(g.Key))
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(ShortLabel))}"));
            Add(SchachtProFieldNames.PrimaereSchaeden, summary);
            Add(SchachtProFieldNames.PrimaereSchaedenAscii, summary);
        }
        else if (anyMaengelfrei)
        {
            Add(SchachtProFieldNames.PrimaereSchaeden, "Maengelfrei");
            Add(SchachtProFieldNames.PrimaereSchaedenAscii, "Maengelfrei");
        }

        // Reihenfolge der Eintraege: Bauteil-Ordnung des PDF-Imports.
        entries = entries
            .OrderBy(e => SchachtProtocolParser.GetComponentOrderIndex(e.Code))
            .ToList();

        return new MappedProtocol(fields, entries, unknown);

        static string ShortLabel(ProtocolEntry entry)
        {
            var text = entry.Beschreibung;
            var separator = text.IndexOf('—');
            return (separator > 0 ? text[..separator] : text).Trim();
        }
    }

    /// <summary>
    /// Mappt die gesetzten Labels einer Sektion auf Protokoll-Eintraege.
    /// Reihenfolge: App-Labelreihenfolge (Mapping-Tabelle), danach unbekannte Labels alphabetisch.
    /// </summary>
    private static List<ProtocolEntry> MapSection(
        string section,
        Dictionary<string, bool> map,
        List<string> unknown,
        ref bool anyMaengelfrei,
        string labelPrefix = "")
    {
        var entries = new List<ProtocolEntry>();
        var setLabels = map
            .Where(kv => kv.Value && !string.IsNullOrWhiteSpace(kv.Key))
            .Select(kv => kv.Key.Trim())
            .ToList();

        if (setLabels.Count == 0)
            return entries;

        if (setLabels.Contains("Mängelfrei", StringComparer.Ordinal))
            anyMaengelfrei = true;

        // Bekannte Labels in App-Reihenfolge, unbekannte ans Ende (alphabetisch).
        var ordered = setLabels
            .OrderBy(label => LabelOrderIndex(section, label))
            .ThenBy(label => label, StringComparer.Ordinal)
            .ToList();

        foreach (var label in ordered)
        {
            var resolved = SchachtProZustandMapper.Resolve(section, label);
            switch (resolved)
            {
                case null:
                    entries.Add(new ProtocolEntry
                    {
                        Code = section,
                        Beschreibung = labelPrefix + label,
                        Source = ProtocolEntrySource.Imported
                    });
                    unknown.Add($"{section}: {label}");
                    break;
                case SchachtProZustandMapper.NoCoding:
                    break;
                case SchachtProZustandMapper.DamageMapping mapping:
                    entries.Add(new ProtocolEntry
                    {
                        Code = section,
                        Beschreibung = labelPrefix.Length == 0
                            ? mapping.FormatBeschreibung(label)
                            : labelPrefix + mapping.FormatBeschreibung(label),
                        Source = ProtocolEntrySource.Imported
                    });
                    break;
            }
        }

        return entries;
    }

    /// <summary>Position des Labels in der App-Anzeigereihenfolge; unbekannte ans Ende.</summary>
    private static int LabelOrderIndex(string section, string label)
    {
        // Globale App-Reihenfolge (ZustandGlobalOrder) — deckt die universellen Sektionen ab.
        var index = Array.FindIndex(GlobalLabelOrder,
            candidate => string.Equals(candidate, label, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : int.MaxValue;
    }

    /// <summary>ZustandGlobalOrder aus ZustandPage.kt + Sonderschluessel der Steighilfe/Tauchbogen.</summary>
    private static readonly string[] GlobalLabelOrder =
    {
        "Mängelfrei",
        "überdeckt",
        "gerissen",
        "Haarrisse",
        "ausgebrochen",
        "lose",
        "korrodiert",
        "Fugen mangelhaft verputzt",
        "mangelhaft unterbetoniert",
        "mangelhaft ausgebildet",
        "kann nicht geöffnet werden",
        "verschraubt",
        "Ablagerungen",
        "Wurzeln",
        "Infiltration",
        "Infiltration Wasser fliesst/spritzt",
        "Verkalkungen",
        "leiter",
        "steigeisen",
        "fehlt",
        "zu kurz",
        "verrostet",
        "defekt",
        "Befestigung mangelhaft",
        "Sprosse(n) gebrochen",
        "vorhanden",
        "nicht notwendig",
        "kann nicht entfernt werden"
    };

    private static void AddGps(List<(string Field, string Value)> fields, ProtocolDto dto)
    {
        if (dto.Lv95East.HasValue)
        {
            fields.Add((SchachtProFieldNames.KoordinateEast,
                dto.Lv95East.Value.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        if (dto.Lv95North.HasValue)
        {
            fields.Add((SchachtProFieldNames.KoordinateNorth,
                dto.Lv95North.Value.ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    private static string FormatAnschluesse(IReadOnlyList<AnschlussDto> anschluesse)
    {
        var lines = new List<string>(anschluesse.Count);
        foreach (var a in anschluesse)
        {
            var parts = new List<string>();
            void Part(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{label}={value.Trim()}");
            }

            Part("Typ", a.Typ);
            Part("Medium", a.Medium);
            Part("DN", a.Dn);
            Part("Tiefe", a.Tiefe);
            Part("Material", a.Material);
            Part("Uhr", a.Uhr);
            Part("Richtung", a.Richtung);
            Part("Rohrform", a.Rohrform);
            Part("Breite", a.Breite);
            Part("Höhe", a.Hoehe);
            lines.Add($"Nr. {a.Nr}: {string.Join(", ", parts)}");
        }

        return string.Join("\n", lines);
    }
}
