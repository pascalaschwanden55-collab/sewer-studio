using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Sanierung;

/// <summary>
/// Hard-Constraint-Filter fuer Sanierungsverfahren VOR der KI-Anfrage.
///
/// Quelle: Knowledge/sanierung/rehabilitation_methods.yaml + products_and_manufacturers.yaml
/// (Marktrecherche AWU 2026-04, VSA QUIK, DIBt-Zulassungen, Hersteller-Datenblaetter)
///
/// Regeln werden hier in C# kodiert um die KI gar nicht erst falsche Verfahren
/// vorschlagen zu lassen. Beispiel: GFK-Liner wird bei Bogen >15 Grad ausgeschlossen,
/// statt darauf zu hoffen dass die KI das selbst weiss.
///
/// Verwendung:
///   var ctx = new HaltungsKontext { DnMm = 200, Material = "Steinzeug", HasBendSevere = true, ... };
///   var codes = new[] { "BABBC", "BCA" };
///   var eval = engine.Evaluate(ctx, codes);
///   eval.Eligible      -> Liste der zulaessigen Verfahren
///   eval.Excluded      -> Liste der ausgeschlossenen mit Begruendung
///   eval.PromptHints   -> Klartext-Hinweise fuer die KI
/// </summary>
public sealed class RehabilitationRulesEngine
{
    private readonly SanierungUserRulesService? _userRules;
    private readonly string? _proceduresJsonPath;

    public RehabilitationRulesEngine(
        SanierungUserRulesService? userRules = null,
        string? proceduresJsonPath = null)
    {
        _userRules = userRules;
        _proceduresJsonPath = proceduresJsonPath;
    }

    // Phase 2.5: damage_groups_by_vsa_code und damage_matrix kommen primaer
    // aus dem JSON (Knowledge/sanierung/rehabilitation_methods.yaml ist die
    // menschlich gepflegte Quelle, JSON ist die maschinen-lesbare Spiegel-Datei).
    // Fallback bleibt der bisherige Hardcode (DefaultDamageGroupByCode +
    // DefaultProcedureDamageMatrix) — defensive Sicherung gegen JSON-Bugs.
    private IReadOnlyDictionary<string, string>? _loadedDamageGroups;
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _loadedDamageMatrix;

    // ── Verfahrens-Katalog ────────────────────────────────────────────────
    // Wird aus Config/rehabilitation_methods.json geladen wenn vorhanden, sonst Fallback
    // auf hartcodierte Liste (DefaultProcedures). Der Konstruktor nimmt optional einen Pfad,
    // sodass UI/ServiceProvider die Datei beim Start angeben koennen.
    private static readonly Lazy<IReadOnlyList<Procedure>> _defaultProceduresLazy =
        new(() => BuildDefaultProcedures());

    public static IReadOnlyList<Procedure> Procedures => _defaultProceduresLazy.Value;

    private IReadOnlyList<Procedure>? _loadedProcedures;
    private IReadOnlyList<Procedure> ActiveProcedures
    {
        get
        {
            if (_loadedProcedures is not null) return _loadedProcedures;
            EnsureLoadedFromJson();
            return _loadedProcedures ?? Procedures;
        }
    }

    /// <summary>Phase 2.5: VSA-Code -> Damage-Group Mapping (primaer JSON, Fallback Hardcode).</summary>
    private IReadOnlyDictionary<string, string> ActiveDamageGroups
    {
        get
        {
            if (_loadedDamageGroups is not null) return _loadedDamageGroups;
            EnsureLoadedFromJson();
            return _loadedDamageGroups ?? DefaultDamageGroupByCode;
        }
    }

    /// <summary>Phase 2.5: Verfahren -> Damage-Group -> Status Matrix (primaer JSON, Fallback Hardcode).</summary>
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ActiveDamageMatrix
    {
        get
        {
            if (_loadedDamageMatrix is not null) return _loadedDamageMatrix;
            EnsureLoadedFromJson();
            return _loadedDamageMatrix ?? DefaultProcedureDamageMatrix;
        }
    }

    /// <summary>
    /// Phase 2.5: einmaliges Laden der drei Sektionen aus JSON.
    /// Schlaegt das Laden fehl oder fehlt der Pfad, bleiben die Felder null —
    /// die Active*-Properties fallen dann auf die Hardcode-Defaults zurueck.
    /// </summary>
    private void EnsureLoadedFromJson()
    {
        if (_loadedProcedures is not null) return; // bereits geladen
        if (string.IsNullOrWhiteSpace(_proceduresJsonPath) || !File.Exists(_proceduresJsonPath))
        {
            _loadedProcedures = Procedures;
            return;
        }

        try
        {
            var json = File.ReadAllText(_proceduresJsonPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (TryLoadProcedures(root, out var procs))
            {
                _loadedProcedures = procs;
                System.Diagnostics.Debug.WriteLine(
                    $"[RehabilitationRulesEngine] Verfahren aus '{Path.GetFileName(_proceduresJsonPath)}' geladen ({procs.Count} Eintraege).");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RehabilitationRulesEngine] WARNUNG: '{_proceduresJsonPath}' enthaelt kein gueltiges 'procedures'-Array - Fallback auf hartcodierte Liste.");
                _loadedProcedures = Procedures;
            }

            if (TryLoadDamageGroups(root, out var groups))
            {
                _loadedDamageGroups = groups;
                System.Diagnostics.Debug.WriteLine(
                    $"[RehabilitationRulesEngine] damage_groups_by_vsa_code aus JSON geladen ({groups.Count} Codes).");
            }

            if (TryLoadDamageMatrix(root, out var matrix))
            {
                _loadedDamageMatrix = matrix;
                System.Diagnostics.Debug.WriteLine(
                    $"[RehabilitationRulesEngine] damage_matrix aus JSON geladen ({matrix.Count} Verfahren).");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[RehabilitationRulesEngine] JSON-Parse-Fehler '{_proceduresJsonPath}': {ex.GetType().Name}: {ex.Message} — Fallback auf Hardcode.");
            _loadedProcedures = Procedures;
        }
    }

    private static IReadOnlyList<Procedure> BuildDefaultProcedures() => new[]
    {
        new Procedure("cipp_inliner",       "CIPP-Schlauchlining (Inliner)",        "Renovierung",  100, 2000)
            { BendCapable = false, MaxBendDegrees = 15 },
        new Procedure("short_liner",        "Kurzliner / Hutprofil / Partliner",    "Reparatur",    100, 800),
        new Procedure("robotic_milling",    "Roboter-Fraesung (Vorbereitung)",      "Vorbereitung", 100, 800)
            { AzCompatible = false },
        new Procedure("robotic_repair",     "Roboter-Spachtelung",                  "Reparatur",    150, 800)
            { AzCompatible = false },
        new Procedure("mechanical_sleeve",  "Edelstahl-Manschette (Quick-Lock)",    "Reparatur",    100, 3000)
            { AzCompatible = true, BendCapable = false },
        new Procedure("berstlining",        "Berstlining (Pipe Bursting)",          "Erneuerung",   100, 500)
            { AzCompatible = false, GfkCompatible = false, SteelCompatible = false },
        new Procedure("close_fit_lining",   "Close-Fit-Lining (PE/PVC)",            "Renovierung",  100, 500)
            { BendCapable = false, MaxBendDegrees = 15 },
        new Procedure("injection_grouting", "Injektion / Verpressung",              "Reparatur",    100, 3000),
        new Procedure("open_excavation",    "Offene Bauweise (Erneuerung Neubau)",  "Erneuerung",   100, 3000),
        new Procedure("shaft_rehabilitation","Schachtsanierung",                    "Renovierung",  800, 2000),
    };

    private static bool TryLoadProcedures(
        System.Text.Json.JsonElement root, out IReadOnlyList<Procedure> procedures)
    {
        procedures = Array.Empty<Procedure>();
        if (!root.TryGetProperty("procedures", out var arr) ||
            arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return false;

        var list = new List<Procedure>();
        foreach (var p in arr.EnumerateArray())
        {
            var id = p.GetProperty("id").GetString() ?? "";
            var name = p.GetProperty("name").GetString() ?? id;
            var cat = p.GetProperty("category").GetString() ?? "Reparatur";
            var dnMin = p.GetProperty("dn_min").GetInt32();
            var dnMax = p.GetProperty("dn_max").GetInt32();
            var proc = new Procedure(id, name, cat, dnMin, dnMax)
            {
                AzCompatible = GetBool(p, "az_compatible", true),
                GfkCompatible = GetBool(p, "gfk_compatible", true),
                SteelCompatible = GetBool(p, "steel_compatible", true),
                BendCapable = GetBool(p, "bend_capable", true),
                MaxBendDegrees = GetInt(p, "max_bend_degrees", 30),
            };
            list.Add(proc);
        }
        if (list.Count == 0) return false;
        procedures = list;
        return true;
    }

    /// <summary>Phase 2.5: laedt VSA-Code -> Damage-Group Mapping aus JSON.</summary>
    private static bool TryLoadDamageGroups(
        System.Text.Json.JsonElement root, out IReadOnlyDictionary<string, string> groups)
    {
        groups = new Dictionary<string, string>();
        if (!root.TryGetProperty("damage_groups_by_vsa_code", out var obj) ||
            obj.ValueKind != System.Text.Json.JsonValueKind.Object)
            return false;

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
        {
            var val = prop.Value.GetString();
            if (string.IsNullOrWhiteSpace(prop.Name) || string.IsNullOrWhiteSpace(val)) continue;
            dict[prop.Name] = val!;
        }
        if (dict.Count == 0) return false;
        groups = dict;
        return true;
    }

    /// <summary>Phase 2.5: laedt Verfahren -> Damage-Group -> Status Matrix aus JSON.</summary>
    private static bool TryLoadDamageMatrix(
        System.Text.Json.JsonElement root,
        out IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> matrix)
    {
        matrix = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        if (!root.TryGetProperty("damage_matrix", out var obj) ||
            obj.ValueKind != System.Text.Json.JsonValueKind.Object)
            return false;

        var dict = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var procEntry in obj.EnumerateObject())
        {
            if (procEntry.Value.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
            var inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var groupEntry in procEntry.Value.EnumerateObject())
            {
                var status = groupEntry.Value.GetString();
                if (string.IsNullOrWhiteSpace(groupEntry.Name) || string.IsNullOrWhiteSpace(status)) continue;
                inner[groupEntry.Name] = status!;
            }
            if (inner.Count > 0)
                dict[procEntry.Name] = inner;
        }
        if (dict.Count == 0) return false;
        matrix = dict;
        return true;
    }

    private static bool GetBool(System.Text.Json.JsonElement el, string prop, bool def) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True ? true
        : el.TryGetProperty(prop, out var vf) && vf.ValueKind == System.Text.Json.JsonValueKind.False ? false
        : def;

    private static int GetInt(System.Text.Json.JsonElement el, string prop, int def) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var i) ? i : def;

    // ── Damage-Group-Mapping (VSA-Code -> Gruppe) ─────────────────────────
    // Phase 2.5: nur noch Hardcode-Fallback. Aktiv genutzt wird ActiveDamageGroups,
    // die primaer JSON liest. Pflege-Quelle: Knowledge/sanierung/rehabilitation_methods.yaml.
    private static readonly Dictionary<string, string> DefaultDamageGroupByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BAA"] = "deformation",
        ["BAB"] = "cracks",
        ["BAC"] = "breaks",
        ["BAE"] = "corrosion",
        ["BAF"] = "corrosion",
        ["BAG"] = "laterals",
        ["BAH"] = "laterals",
        ["BAI"] = "obstructions",
        ["BAJ"] = "joints",
        ["BBA"] = "obstructions",
        ["BBB"] = "obstructions",
        ["BBC"] = "obstructions",
        ["BBD"] = "obstructions",
        ["BBE"] = "obstructions",
        ["BBF"] = "infiltration",
        ["BBG"] = "exfiltration",
        ["BCA"] = "laterals",
    };

    // ── Verfahren -> behandelte Damage-Groups ─────────────────────────────
    // eligible | conditional | excluded
    // Phase 2.5: nur noch Hardcode-Fallback. Aktiv genutzt wird ActiveDamageMatrix,
    // die primaer JSON liest. Pflege-Quelle: Knowledge/sanierung/rehabilitation_methods.yaml.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> DefaultProcedureDamageMatrix = BuildDefaultMatrix();

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildDefaultMatrix()
    {
        var raw = new Dictionary<string, Dictionary<string, string>>
    {
        ["cipp_inliner"] = new() {
            ["cracks"]="eligible", ["joints"]="eligible", ["infiltration"]="eligible",
            ["exfiltration"]="eligible", ["laterals"]="conditional", ["obstructions"]="excluded",
            ["deformation"]="conditional", ["breaks"]="excluded", ["corrosion"]="eligible" },
        ["short_liner"] = new() {
            ["cracks"]="eligible", ["joints"]="eligible", ["infiltration"]="eligible",
            ["exfiltration"]="eligible", ["laterals"]="eligible", ["obstructions"]="excluded",
            ["deformation"]="excluded", ["breaks"]="conditional", ["corrosion"]="conditional" },
        ["robotic_milling"] = new() {
            ["obstructions"]="eligible", ["laterals"]="eligible" },
        ["robotic_repair"] = new() {
            ["cracks"]="conditional", ["joints"]="conditional", ["infiltration"]="conditional",
            ["exfiltration"]="conditional", ["laterals"]="eligible", ["corrosion"]="conditional" },
        ["mechanical_sleeve"] = new() {
            ["joints"]="eligible", ["cracks"]="conditional", ["infiltration"]="eligible",
            ["exfiltration"]="eligible", ["laterals"]="conditional", ["breaks"]="conditional",
            ["corrosion"]="conditional" },
        ["berstlining"] = new() {
            ["cracks"]="eligible", ["joints"]="eligible", ["breaks"]="eligible",
            ["deformation"]="eligible", ["corrosion"]="eligible", ["laterals"]="conditional",
            ["infiltration"]="eligible", ["exfiltration"]="eligible" },
        ["close_fit_lining"] = new() {
            ["cracks"]="eligible", ["joints"]="eligible", ["infiltration"]="eligible",
            ["exfiltration"]="eligible", ["corrosion"]="eligible", ["deformation"]="conditional",
            ["laterals"]="conditional" },
        ["injection_grouting"] = new() {
            ["infiltration"]="eligible", ["cracks"]="conditional", ["joints"]="conditional",
            ["exfiltration"]="conditional" },
        ["open_excavation"] = new() {
            ["cracks"]="eligible", ["joints"]="eligible", ["infiltration"]="eligible",
            ["exfiltration"]="eligible", ["laterals"]="eligible", ["obstructions"]="eligible",
            ["deformation"]="eligible", ["breaks"]="eligible", ["corrosion"]="eligible" },
        ["shaft_rehabilitation"] = new() {
            ["corrosion"]="eligible", ["cracks"]="conditional", ["infiltration"]="eligible" },
        };
        // Wrap in IReadOnlyDictionary fuer das Feld
        var wrapped = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in raw) wrapped[kvp.Key] = kvp.Value;
        return wrapped;
    }

    // ── Hauptmethode ──────────────────────────────────────────────────────
    public RulesEvaluation Evaluate(HaltungsKontext ctx, IReadOnlyList<string> vsaCodes)
    {
        var result = new RulesEvaluation();
        var damageGroups = ResolveDamageGroups(vsaCodes);
        result.DamageGroups = damageGroups;

        var isAz = IsMaterial(ctx.Material, "asbest", "az ", "asbestzement");
        var isGfk = IsMaterial(ctx.Material, "gfk", "glasfaser");
        var isSteel = IsMaterial(ctx.Material, "stahl", "guss");

        foreach (var p in ActiveProcedures)
        {
            var reasons = new List<string>();

            // 1. DN-Range-Check
            if (ctx.DnMm.HasValue && (ctx.DnMm.Value < p.DnMin || ctx.DnMm.Value > p.DnMax))
                reasons.Add($"DN {ctx.DnMm.Value} ausserhalb des Verfahrensbereichs ({p.DnMin}-{p.DnMax} mm)");

            // 2. Material-Inkompatibilitaet
            if (isAz && !p.AzCompatible)
                reasons.Add("Material Asbestzement: Verfahren nicht zulaessig (Faserfreisetzung / SUVA Bauarbeiterschutzverordnung)");
            if (isGfk && !p.GfkCompatible)
                reasons.Add("Material GFK: Verfahren nicht anwendbar (kein Berstkopf-Eingriff moeglich)");
            if (isSteel && !p.SteelCompatible)
                reasons.Add("Material Stahl/Guss: Verfahren nicht anwendbar");

            // 3. Bogen-Check (nur fuer Liner-Verfahren mit BendCapable=false)
            if (ctx.HasBendSevere && p.RequiresStraight)
                reasons.Add($"Starker Bogen (>{p.MaxBendDegrees}°): {p.Name} ist nicht bogengaengig - Brawoliner-Familie (Textil) erwaegen");
            else if (ctx.HasBendModerate && p.MaxBendDegrees < 30 && p.RequiresStraight)
                reasons.Add($"Bogen erfordert flexiblen Liner: GFK ungeeignet, Nadelfilz oder Brawoliner verwenden");

            // 4. Damage-Group-Match: Verfahren muss mind. 1 vorhandene Group abdecken
            // Phase 2.5: ActiveDamageMatrix kommt primaer aus JSON.
            var matrix = ActiveDamageMatrix.GetValueOrDefault(p.Id)
                ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
            var addressedGroups = damageGroups
                .Select(g => new { Group = g, Status = matrix.GetValueOrDefault(g, "n/a") })
                .Where(x => x.Status == "eligible" || x.Status == "conditional")
                .ToList();

            if (damageGroups.Count > 0 && addressedGroups.Count == 0)
                reasons.Add($"Verfahren behandelt keine der vorhandenen Schadensgruppen ({string.Join(", ", damageGroups)})");

            // 5. Damage-Group-spezifische Excluded-Match (z.B. cracks bei robotic_milling)
            foreach (var g in damageGroups)
            {
                if (matrix.TryGetValue(g, out var status) && status == "excluded")
                    reasons.Add($"Schadensgruppe '{g}' bei {p.Name} explizit ausgeschlossen");
            }

            // 6. Bruch / Einsturz: nur Erneuerung
            if (damageGroups.Contains("breaks") &&
                p.Id is "cipp_inliner" or "close_fit_lining" or "injection_grouting")
            {
                reasons.Add("Einsturzgefahr (BAC): nur Erneuerung (Berstlining / Open Excavation) zulaessig");
            }

            // 7. Tiefe Zustandsklasse + nicht-strukturelles Verfahren?
            // (entfaellt - das ist kein Hard-Constraint, nur Empfehlung der KI)

            // Klassifizierung
            if (reasons.Count == 0)
            {
                var conditional = addressedGroups.Any(x => x.Status == "conditional")
                    && !addressedGroups.Any(x => x.Status == "eligible");
                if (conditional)
                    result.Conditional.Add(new ProcedureMatch(p, "bedingt geeignet (siehe Voraussetzungen)"));
                else
                    result.Eligible.Add(new ProcedureMatch(p, "geeignet"));
            }
            else
            {
                result.Excluded.Add(new ProcedureMatch(p, string.Join("; ", reasons)));
            }
        }

        // ===== USER-REGELN auswerten (im UI gepflegt) =====
        // Diese ueberschreiben/ergaenzen die Default-Regeln. Match -> Verfahren wird
        // in Excluded verschoben (auch wenn vorher Eligible).
        if (_userRules is not null)
        {
            try
            {
                var userRules = _userRules.GetActiveRules();
                foreach (var rule in userRules)
                {
                    if (!MatchesUserRule(rule, ctx, vsaCodes, damageGroups)) continue;

                    foreach (var procId in rule.ExcludeProcedureIds)
                    {
                        // Aus Eligible/Conditional entfernen, in Excluded verschieben
                        var fromEligible = result.Eligible.FirstOrDefault(e =>
                            string.Equals(e.Procedure.Id, procId, StringComparison.OrdinalIgnoreCase));
                        if (fromEligible is not null) result.Eligible.Remove(fromEligible);

                        var fromConditional = result.Conditional.FirstOrDefault(e =>
                            string.Equals(e.Procedure.Id, procId, StringComparison.OrdinalIgnoreCase));
                        if (fromConditional is not null) result.Conditional.Remove(fromConditional);

                        var proc = (fromEligible ?? fromConditional)?.Procedure
                            ?? ActiveProcedures.FirstOrDefault(p => string.Equals(p.Id, procId, StringComparison.OrdinalIgnoreCase));
                        if (proc is null) continue;

                        // Schon in Excluded? Reason ergaenzen statt doppeln
                        var existing = result.Excluded.FirstOrDefault(e =>
                            string.Equals(e.Procedure.Id, procId, StringComparison.OrdinalIgnoreCase));
                        var ruleReason = $"[Eigene Regel '{rule.Name}']: {rule.Reason}";
                        if (existing is null)
                            result.Excluded.Add(new ProcedureMatch(proc, ruleReason));
                        else
                        {
                            result.Excluded.Remove(existing);
                            result.Excluded.Add(new ProcedureMatch(proc, existing.Reason + " | " + ruleReason));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RulesEngine] User-Regeln Auswertung fehlgeschlagen: {ex.Message}");
            }
        }

        // Prompt-Hints generieren
        result.PromptHints = BuildPromptHints(ctx, damageGroups, result);
        return result;
    }

    /// <summary>Prueft ob eine User-Regel auf den Kontext zutrifft (alle gesetzten Bedingungen muessen matchen).</summary>
    private static bool MatchesUserRule(
        SanierungUserRule rule, HaltungsKontext ctx,
        IReadOnlyList<string> vsaCodes, IReadOnlyList<string> damageGroups)
    {
        var c = rule.Conditions;
        var mat = ctx.Material?.ToLowerInvariant() ?? "";

        if (!string.IsNullOrWhiteSpace(c.MaterialContains)
            && !mat.Contains(c.MaterialContains.ToLowerInvariant())) return false;

        if (!string.IsNullOrWhiteSpace(c.MaterialNotContains)
            && mat.Contains(c.MaterialNotContains.ToLowerInvariant())) return false;

        if (c.DnMin.HasValue && (!ctx.DnMm.HasValue || ctx.DnMm.Value < c.DnMin.Value)) return false;
        if (c.DnMax.HasValue && (!ctx.DnMm.HasValue || ctx.DnMm.Value > c.DnMax.Value)) return false;

        if (c.BendDegreesMin.HasValue)
        {
            // Verwende HasBendSevere/Moderate als Indikator; konservativ: severe = >=30, moderate = >=15
            var maxBend = ctx.HasBendSevere ? 30.0 : ctx.HasBendModerate ? 15.0 : 0.0;
            if (maxBend < c.BendDegreesMin.Value) return false;
        }

        if (c.DamageGroupAnyOf is { Count: > 0 }
            && !c.DamageGroupAnyOf.Any(g => damageGroups.Contains(g, StringComparer.OrdinalIgnoreCase)))
            return false;

        if (!string.IsNullOrWhiteSpace(c.VsaCodeStartsWith)
            && !vsaCodes.Any(code => code?.StartsWith(c.VsaCodeStartsWith, StringComparison.OrdinalIgnoreCase) == true))
            return false;

        if (c.ZustandsklasseMax.HasValue
            && (!ctx.Zustandsklasse.HasValue || ctx.Zustandsklasse.Value > c.ZustandsklasseMax.Value))
            return false;

        if (c.Groundwater.HasValue && ctx.Grundwasser != c.Groundwater.Value) return false;

        if (!string.IsNullOrWhiteSpace(c.NutzungsartContains))
        {
            var nutz = ctx.Nutzungsart?.ToLowerInvariant() ?? "";
            if (!nutz.Contains(c.NutzungsartContains.ToLowerInvariant())) return false;
        }

        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    // Phase 2.5: Instanzmethode (statt static), damit ActiveDamageGroups (aus JSON
    // oder Hardcode-Fallback) genutzt wird.
    private IReadOnlyList<string> ResolveDamageGroups(IReadOnlyList<string> codes)
    {
        var damageGroups = ActiveDamageGroups;
        var groups = new HashSet<string>();
        foreach (var rawCode in codes)
        {
            if (string.IsNullOrWhiteSpace(rawCode)) continue;
            var norm = Regex.Replace(rawCode.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
            if (norm.Length < 3) continue;
            // Versuche zuerst Vollcode, dann 3-Char-Hauptcode
            if (damageGroups.TryGetValue(norm, out var g)) { groups.Add(g); continue; }
            var main3 = norm.Substring(0, 3);
            if (damageGroups.TryGetValue(main3, out var g2)) groups.Add(g2);
        }
        return groups.ToList();
    }

    private static bool IsMaterial(string? material, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(material)) return false;
        var m = material.ToLowerInvariant();
        return keywords.Any(k => m.Contains(k));
    }

    private static List<string> BuildPromptHints(
        HaltungsKontext ctx, IReadOnlyList<string> groups, RulesEvaluation eval)
    {
        var hints = new List<string>();
        hints.Add($"Schadensgruppen: {string.Join(", ", groups)}");
        if (ctx.HasBendSevere) hints.Add("ACHTUNG: Haltung hat starken Bogen (>30°) - nur bogenfaehige Liner (Brawoliner) zulaessig.");
        else if (ctx.HasBendModerate) hints.Add("Hinweis: Haltung hat moderaten Bogen - GFK-Liner ungeeignet, Nadelfilz/Brawoliner verwenden.");
        if (IsMaterial(ctx.Material, "asbest", "az ", "asbestzement"))
            hints.Add("ACHTUNG: Asbestzement-Material - keine Fraesung, kein Bersten, kein Aufgraben ohne SUVA-Konzept.");
        if (eval.Excluded.Count > 0)
            hints.Add($"AUSGESCHLOSSENE Verfahren ({eval.Excluded.Count}): {string.Join("; ", eval.Excluded.Take(3).Select(e => e.Procedure.Name))}...");
        return hints;
    }
}

// ── Datenmodell ────────────────────────────────────────────────────────────

public sealed record HaltungsKontext
{
    public double? DnMm { get; init; }
    public string? Material { get; init; }
    public double? LaengeM { get; init; }
    public string? Nutzungsart { get; init; }
    public bool? Grundwasser { get; init; }

    /// <summary>True wenn Haltung einen starken Bogen (>30 Grad) hat.</summary>
    public bool HasBendSevere { get; init; }

    /// <summary>True wenn Haltung einen moderaten Bogen (15-30 Grad) hat.</summary>
    public bool HasBendModerate { get; init; }

    public int? AnzahlAnschluesse { get; init; }
    public int? Zustandsklasse { get; init; }
}

public sealed class Procedure
{
    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
    public int DnMin { get; }
    public int DnMax { get; }

    public bool AzCompatible { get; init; } = true;
    public bool GfkCompatible { get; init; } = true;
    public bool SteelCompatible { get; init; } = true;
    public bool BendCapable { get; init; } = true;
    public int MaxBendDegrees { get; init; } = 30;

    /// <summary>Wenn true, kein Bogen >MaxBendDegrees zulaessig.</summary>
    public bool RequiresStraight => !BendCapable;

    public Procedure(string id, string name, string category, int dnMin, int dnMax)
    {
        Id = id;
        Name = name;
        Category = category;
        DnMin = dnMin;
        DnMax = dnMax;
    }
}

public sealed class RulesEvaluation
{
    public IReadOnlyList<string> DamageGroups { get; set; } = Array.Empty<string>();
    public List<ProcedureMatch> Eligible { get; } = new();
    public List<ProcedureMatch> Conditional { get; } = new();
    public List<ProcedureMatch> Excluded { get; } = new();
    public List<string> PromptHints { get; set; } = new();

    /// <summary>Vereinigte Liste aller erlaubten Verfahren (eligible + conditional).</summary>
    public IEnumerable<ProcedureMatch> Allowed => Eligible.Concat(Conditional);
}

public sealed record ProcedureMatch(Procedure Procedure, string Reason);
