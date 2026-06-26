using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingHaltungslaengeEnsureWorkflowActions(
    Func<CodingHaltungslaengeEnsureService> CreateService);

public static class CodingHaltungslaengeEnsureWorkflow
{
    public static void Ensure(
        HaltungRecord record,
        double? overlayPipeLengthMeters)
        => Ensure(
            record,
            overlayPipeLengthMeters,
            new CodingHaltungslaengeEnsureWorkflowActions(
                CreateService: CodingHaltungslaengeEnsureServiceFactory.Create));

    public static void Ensure(
        HaltungRecord record,
        double? overlayPipeLengthMeters,
        CodingHaltungslaengeEnsureWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        service.Ensure(record, overlayPipeLengthMeters);
    }
}
