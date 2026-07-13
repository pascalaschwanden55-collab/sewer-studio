using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportVsaEvaluationActions(
    Action<string> SetProgress,
    Action<string> AppendSummary);

/// <summary>Führt die VSA-Bewertung nach einem Import im Hintergrund aus.</summary>
internal sealed class ImportVsaEvaluationController
{
    private readonly IVsaEvaluationService _service;
    private readonly ILogger _logger;

    public ImportVsaEvaluationController(IVsaEvaluationService service, ILogger logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(
        Project project,
        string sourceLabel,
        ImportVsaEvaluationActions actions)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetProgress($"{sourceLabel}: VSA-Zustandsbewertung wird berechnet...");
        var result = await Task.Run(() => _service.Evaluate(project));
        if (result.Ok)
        {
            actions.AppendSummary($"\nVSA-Bewertung: {project.Data.Count} Haltungen bewertet");
            return;
        }

        _logger.LogWarning(
            "VSA-Bewertung fehlgeschlagen ({Fehlercode}): {Fehler}",
            result.ErrorCode,
            result.ErrorMessage);
        actions.AppendSummary(
            "\nVSA-Bewertung fehlgeschlagen. Technische Details stehen im Tageslog.");
    }
}
