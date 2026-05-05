using System;
using System.IO;
using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Infrastructure.Devis;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.Sanierung;

namespace AuswertungPro.Next.UI.Modules;

/// <summary>
/// Phase 5.2.C: Modul fuer Devis- und Sanierungs-Services.
/// Konfiguriert ueber JSON-Dateien im AppContext.BaseDirectory/Config:
///   devis_mappings.json, submission_positionen.json, historische_sanierungen.json,
///   sanierung_user_rules.json, rehabilitation_methods.json
///
/// Reihenfolge der Konstruktion ist wichtig:
///   1. DevisMappingService + SubmissionsPositionService (unabhaengig)
///   2. HistorischeSanierungen (unabhaengig)
///   3. MarktdatenImport (braucht SubmissionsPositions + HistorischeSanierungen)
///   4. SanierungUserRules (unabhaengig)
///   5. RehabRulesEngine (braucht SanierungUserRules)
///   6. DevisGenerator (braucht DevisMappingService)
/// </summary>
internal static class DevisSanierungModule
{
    public sealed record Services(
        IDevisGenerator DevisGenerator,
        DevisExcelExporter DevisExcelExporter,
        SubmissionsPositionService SubmissionsPositions,
        HistorischeSanierungenService HistorischeSanierungen,
        MarktdatenImportService MarktdatenImport,
        RehabilitationRulesEngine RehabRulesEngine,
        SanierungUserRulesService SanierungUserRules);

    public static Services Configure()
    {
        var configDir = Path.Combine(AppContext.BaseDirectory, "Config");

        var devisMappingService = new DevisMappingService(
            Path.Combine(configDir, "devis_mappings.json"));

        var submissionsPositions = new SubmissionsPositionService(
            Path.Combine(configDir, "submission_positionen.json"));

        var historischeSanierungen = new HistorischeSanierungenService(
            Path.Combine(configDir, "historische_sanierungen.json"));

        var marktdatenImport = new MarktdatenImportService(
            configDir, submissionsPositions, historischeSanierungen);

        var sanierungUserRules = new SanierungUserRulesService(
            Path.Combine(configDir, "sanierung_user_rules.json"));

        var rehabRulesEngine = new RehabilitationRulesEngine(
            sanierungUserRules,
            Path.Combine(configDir, "rehabilitation_methods.json"));

        return new Services(
            DevisGenerator: new DevisGenerator(devisMappingService),
            DevisExcelExporter: new DevisExcelExporter(),
            SubmissionsPositions: submissionsPositions,
            HistorischeSanierungen: historischeSanierungen,
            MarktdatenImport: marktdatenImport,
            RehabRulesEngine: rehabRulesEngine,
            SanierungUserRules: sanierungUserRules);
    }
}
