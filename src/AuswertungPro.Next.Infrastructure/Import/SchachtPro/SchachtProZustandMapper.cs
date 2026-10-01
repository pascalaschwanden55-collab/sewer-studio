namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>
/// Stufe B: Norm-Mapping der SchachtPro-Zustandslabels auf VSA-KEK/EN-13508-2
/// D-Codes (Hauptcode + Charakterisierung1 + Schadensklasse).
///
/// Die Label-Strings stammen aus der App (ZustandPage.kt) und sind autoritativ;
/// die Codierung folgt docs/SchachtPro_XTF_Export_Konzept.xlsx.
/// Das Mapping ist sektionsabhaengig: "defekt"/"fehlt" bedeuten bei
/// Leiter/Steigeisen etwas anderes als beim Tauchbogen.
/// Unbekannte Labels werden nicht geraten: sie gehen als Klartext durch (Uncertain).
/// </summary>
internal static class SchachtProZustandMapper
{
    /// <summary>Ergebnis eines gemappten Labels.</summary>
    /// <param name="Code">VSA-Hauptcode (z.B. "DAB").</param>
    /// <param name="Charakterisierung">Charakterisierung1 (z.B. "B"), null = keine.</param>
    /// <param name="Schadensklasse">"K1"/"K2"/"K3" oder "Z" (nicht beurteilbar).</param>
    internal sealed record DamageMapping(string Code, string? Charakterisierung, string Schadensklasse)
    {
        /// <summary>"gerissen" + DAB/B/K2 -> "gerissen — DAB-B, K2"; ohne Char. -> "fehlt — DAP, K2".</summary>
        internal string FormatBeschreibung(string label)
        {
            var codePart = string.IsNullOrEmpty(Charakterisierung) ? Code : $"{Code}-{Charakterisierung}";
            return $"{label} — {codePart}, {Schadensklasse}";
        }
    }

    /// <summary>Label, das bewusst keine Codierung erzeugt (Inventar-Info oder Mängelfrei).</summary>
    internal sealed record NoCoding;

    /// <summary>Universelle Labels (ZustandGlobalOrder der App). Reihenfolge = App-Anzeigereihenfolge.</summary>
    private static readonly (string Label, DamageMapping? Mapping)[] UniversalLabels =
    {
        // Maengelfrei: kein Eintrag (wenn alleinig gesetzt: keine Schadenseintraege)
        ("Mängelfrei", null),
        ("überdeckt", new DamageMapping("DXX", null, "Z")),
        ("gerissen", new DamageMapping("DAB", "B", "K2")),
        ("Haarrisse", new DamageMapping("DAB", "A", "K3")),
        ("ausgebrochen", new DamageMapping("DAC", "B", "K1")),
        ("lose", new DamageMapping("DAH", null, "K2")),
        ("korrodiert", new DamageMapping("DAI", "A", "K2")),
        ("Fugen mangelhaft verputzt", new DamageMapping("DAD", "A", "K2")),
        ("mangelhaft unterbetoniert", new DamageMapping("DAD", "B", "K2")),
        ("mangelhaft ausgebildet", new DamageMapping("DAP", null, "K3")),
        ("kann nicht geöffnet werden", new DamageMapping("DXX", null, "Z")),
        // verschraubt: Inventar-Info, kein Schaden
        ("verschraubt", null),
        ("Ablagerungen", new DamageMapping("DAK", "A", "K3")),
        ("Wurzeln", new DamageMapping("DAL", "A", "K2")),
        ("Infiltration", new DamageMapping("DAM", "A", "K2")),
        ("Infiltration Wasser fliesst/spritzt", new DamageMapping("DAM", "C", "K1")),
        ("Verkalkungen", new DamageMapping("DAK", "B", "K3"))
    };

    /// <summary>Steighilfe (Sektion "Leiter/Steigeisen"). "leiter"/"steigeisen" sind Inventar (Radio).</summary>
    private static readonly (string Label, DamageMapping? Mapping)[] SteighilfeLabels =
    {
        ("leiter", null),
        ("steigeisen", null),
        ("fehlt", new DamageMapping("DAN", "A", "K1")),
        ("zu kurz", new DamageMapping("DAN", "C", "K2")),
        ("verrostet", new DamageMapping("DAI", "B", "K2")),
        ("defekt", new DamageMapping("DAN", "B", "K1")),
        ("Befestigung mangelhaft", new DamageMapping("DAN", "D", "K2")),
        ("Sprosse(n) gebrochen", new DamageMapping("DAN", "B", "K1"))
    };

    /// <summary>Tauchbogen. "vorhanden"/"nicht notwendig" erzeugen nichts.</summary>
    private static readonly (string Label, DamageMapping? Mapping)[] TauchbogenLabels =
    {
        ("vorhanden", null),
        ("nicht notwendig", null),
        ("fehlt", new DamageMapping("DAP", null, "K2")),
        ("defekt", new DamageMapping("DAC", null, "K2")),
        ("kann nicht entfernt werden", new DamageMapping("DAO", null, "K3"))
    };

    /// <summary>App-Sektionen mit universellen Labels (ZustandSections aus ZustandPage.kt).</summary>
    internal static readonly string[] UniversalSections =
    {
        "Schacht",
        "Schachtdeckel",
        "Deckelrahmen",
        "Schachthals",
        "Konus",
        "Schachtrohr",
        "Bankett",
        "Durchlaufrinne"
    };

    internal const string SectionSteighilfe = "Leiter/Steigeisen";
    internal const string SectionTauchbogen = "Tauchbogen";
    internal const string SectionAnschluss = "Anschluss";

    /// <summary>
    /// Sucht die Codierung fuer <paramref name="label"/> in der gegebenen Sektion.
    /// Rueckgabe null = unbekanntes Label (Klartext durchreichen, Uncertain melden).
    /// Rueckgabe <see cref="NoCoding"/> = bekanntes Label ohne Codierung.
    /// Der Vergleich ist Gross-/Kleinschreibung-tolerant (App-Daten enthalten z.B.
    /// "infiltration" klein bei Anschluessen), exakte Treffer gewinnen zuerst.
    /// </summary>
    internal static object? Resolve(string section, string label)
    {
        var table = TableFor(section);
        if (table is null)
            return null;

        foreach (var (candidate, mapping) in table)
        {
            if (string.Equals(candidate, label, StringComparison.Ordinal))
                return (object?)mapping ?? new NoCoding();
        }

        foreach (var (candidate, mapping) in table)
        {
            if (string.Equals(candidate, label, StringComparison.OrdinalIgnoreCase))
                return (object?)mapping ?? new NoCoding();
        }

        return null;
    }

    private static (string Label, DamageMapping? Mapping)[]? TableFor(string section)
        => section switch
        {
            SectionSteighilfe => SteighilfeLabels,
            SectionTauchbogen => TauchbogenLabels,
            SectionAnschluss => UniversalLabels,
            _ => UniversalSections.Contains(section, StringComparer.Ordinal) ? UniversalLabels : null
        };
}
