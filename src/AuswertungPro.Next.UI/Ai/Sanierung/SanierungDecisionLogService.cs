// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Sanierung;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

/// <summary>
/// Phase 5.5: Decision-Log fuer Sanierungs-Entscheidungen.
///
/// Audit A7 (Konsens 3/3): "Jeder Sanierungsvorschlag soll speichern: erkannte
/// Schaeden, angewandte Regeln, ausgeschlossene Massnahmen mit Grund, erlaubte
/// Massnahmen, Datenstand / Knowledge-Version, KI-Modell und Prompt-Version
/// (falls KI beteiligt war), manuelle Aenderungen des Users."
///
/// Dieser Service speichert eine RulesEvaluation als persistenten Eintrag
/// in <c>SanierungDecisionLog</c>. Aufrufer rufen <see cref="LogEvaluation"/>
/// nach jedem Engine.Evaluate(...). Der Service ist optional — wer kein
/// Decision-Log braucht, ruft ihn einfach nicht auf.
///
/// Nutzt <see cref="KnowledgeBaseWriter"/> (Phase 2.2) fuer serialisierte Writes.
/// </summary>
public sealed class SanierungDecisionLogService
{
    private readonly KnowledgeBaseContext _db;
    private readonly KnowledgeBaseWriter _writer;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SanierungDecisionLogService(KnowledgeBaseContext db, KnowledgeBaseWriter? writer = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _writer = writer ?? new KnowledgeBaseWriter(db);
    }

    /// <summary>
    /// Schreibt einen neuen Eintrag ins Decision-Log. Gibt die generierte
    /// DecisionId zurueck.
    /// </summary>
    /// <param name="context">Haltungs-/Case-Bezeichner fuer Audit-Trail (z.B. "AWU.Hauptstrasse.H42").</param>
    /// <param name="vsaCodes">Eingabe-Codes der Evaluation.</param>
    /// <param name="eval">Resultat von RehabilitationRulesEngine.Evaluate(...).</param>
    /// <param name="knowledgeVersion">JSON/YAML-Schema-Version, z.B. "1.1".</param>
    /// <param name="runId">Optional: Verknuepfung zu TrainingRuns (Phase 4.4).</param>
    /// <param name="notes">Freitext (z.B. "User hat Empfehlung uebersteuert").</param>
    public string LogEvaluation(
        string context,
        IReadOnlyList<string> vsaCodes,
        RulesEvaluation eval,
        string knowledgeVersion = "",
        string? runId = null,
        string notes = "")
    {
        ArgumentNullException.ThrowIfNull(eval);
        var decisionId = Guid.NewGuid().ToString("N");
        var createdUtc = DateTime.UtcNow.ToString("O");

        var vsaCodesJson = JsonSerializer.Serialize(vsaCodes ?? Array.Empty<string>(), JsonOpts);
        var damageGroupsJson = JsonSerializer.Serialize(eval.DamageGroups, JsonOpts);
        var eligibleJson = JsonSerializer.Serialize(SerializeMatches(eval.Eligible), JsonOpts);
        var conditionalJson = JsonSerializer.Serialize(SerializeMatches(eval.Conditional), JsonOpts);
        var excludedJson = JsonSerializer.Serialize(SerializeMatches(eval.Excluded), JsonOpts);
        var promptHintsJson = JsonSerializer.Serialize(eval.PromptHints, JsonOpts);

        _writer.Execute(_ =>
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO SanierungDecisionLog
                    (DecisionId, CreatedUtc, Context, VsaCodesJson, DamageGroupsJson,
                     EligibleJson, ConditionalJson, ExcludedJson, PromptHintsJson,
                     KnowledgeVersion, RunId, Notes)
                VALUES ($id, $created, $ctx, $vc, $dg, $el, $co, $ex, $ph, $kv, $run, $notes)
                """;
            cmd.Parameters.AddWithValue("$id",      decisionId);
            cmd.Parameters.AddWithValue("$created", createdUtc);
            cmd.Parameters.AddWithValue("$ctx",     context ?? "");
            cmd.Parameters.AddWithValue("$vc",      vsaCodesJson);
            cmd.Parameters.AddWithValue("$dg",      damageGroupsJson);
            cmd.Parameters.AddWithValue("$el",      eligibleJson);
            cmd.Parameters.AddWithValue("$co",      conditionalJson);
            cmd.Parameters.AddWithValue("$ex",      excludedJson);
            cmd.Parameters.AddWithValue("$ph",      promptHintsJson);
            cmd.Parameters.AddWithValue("$kv",      knowledgeVersion ?? "");
            cmd.Parameters.AddWithValue("$run",    (object?)runId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$notes",   notes ?? "");
            cmd.ExecuteNonQuery();
        });

        return decisionId;
    }

    /// <summary>
    /// Liefert die letzten N Eintraege aus dem Decision-Log (neueste zuerst).
    /// </summary>
    public IReadOnlyList<DecisionLogEntry> GetRecent(int limit = 50)
    {
        if (limit < 1) limit = 1;
        var result = new List<DecisionLogEntry>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT DecisionId, CreatedUtc, Context, VsaCodesJson, DamageGroupsJson,
                   EligibleJson, ConditionalJson, ExcludedJson, PromptHintsJson,
                   KnowledgeVersion, RunId, Notes
            FROM SanierungDecisionLog
            ORDER BY CreatedUtc DESC
            LIMIT $lim
            """;
        cmd.Parameters.AddWithValue("$lim", limit);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            result.Add(ReadEntry(rdr));
        return result;
    }

    /// <summary>Sucht einen Eintrag per DecisionId.</summary>
    public DecisionLogEntry? GetById(string decisionId)
    {
        if (string.IsNullOrWhiteSpace(decisionId)) return null;
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT DecisionId, CreatedUtc, Context, VsaCodesJson, DamageGroupsJson,
                   EligibleJson, ConditionalJson, ExcludedJson, PromptHintsJson,
                   KnowledgeVersion, RunId, Notes
            FROM SanierungDecisionLog
            WHERE DecisionId = $id
            """;
        cmd.Parameters.AddWithValue("$id", decisionId);
        using var rdr = cmd.ExecuteReader();
        return rdr.Read() ? ReadEntry(rdr) : null;
    }

    /// <summary>Liefert Eintraege fuer einen Kontext (Haltungs-/Case-Filter).</summary>
    public IReadOnlyList<DecisionLogEntry> GetByContext(string context, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(context)) return Array.Empty<DecisionLogEntry>();
        if (limit < 1) limit = 1;
        var result = new List<DecisionLogEntry>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT DecisionId, CreatedUtc, Context, VsaCodesJson, DamageGroupsJson,
                   EligibleJson, ConditionalJson, ExcludedJson, PromptHintsJson,
                   KnowledgeVersion, RunId, Notes
            FROM SanierungDecisionLog
            WHERE Context = $ctx
            ORDER BY CreatedUtc DESC
            LIMIT $lim
            """;
        cmd.Parameters.AddWithValue("$ctx", context);
        cmd.Parameters.AddWithValue("$lim", limit);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            result.Add(ReadEntry(rdr));
        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static IEnumerable<object> SerializeMatches(IEnumerable<ProcedureMatch> matches) =>
        matches.Select(m => (object)new
        {
            id = m.Procedure.Id,
            name = m.Procedure.Name,
            category = m.Procedure.Category,
            reason = m.Reason
        });

    private static DecisionLogEntry ReadEntry(Microsoft.Data.Sqlite.SqliteDataReader rdr)
    {
        return new DecisionLogEntry(
            DecisionId:       rdr.GetString(0),
            CreatedUtc:       rdr.GetString(1),
            Context:          rdr.GetString(2),
            VsaCodesJson:     rdr.GetString(3),
            DamageGroupsJson: rdr.GetString(4),
            EligibleJson:     rdr.GetString(5),
            ConditionalJson:  rdr.GetString(6),
            ExcludedJson:     rdr.GetString(7),
            PromptHintsJson:  rdr.GetString(8),
            KnowledgeVersion: rdr.GetString(9),
            RunId:            rdr.IsDBNull(10) ? null : rdr.GetString(10),
            Notes:            rdr.GetString(11));
    }
}

/// <summary>
/// Phase 5.5: Decision-Log-Eintrag (Lese-DTO). Listen sind als JSON-String
/// gespeichert — Konsumenten koennen sie via JsonSerializer.Deserialize
/// in typisierte Listen umwandeln, je nach Bedarf.
/// </summary>
public sealed record DecisionLogEntry(
    string DecisionId,
    string CreatedUtc,
    string Context,
    string VsaCodesJson,
    string DamageGroupsJson,
    string EligibleJson,
    string ConditionalJson,
    string ExcludedJson,
    string PromptHintsJson,
    string KnowledgeVersion,
    string? RunId,
    string Notes);
