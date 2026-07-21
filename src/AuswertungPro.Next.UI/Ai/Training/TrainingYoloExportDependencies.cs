using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Einmalig am Programmstart zusammengesetzter Anschluss fuer den plan-gesteuerten
/// YOLO-Export. Die UI kennt nur noch den Koordinator; seine Datenwurzeln sind
/// unveraenderlich in der gemeinsamen Runtime gebunden.
/// </summary>
public sealed class TrainingYoloExportDependencies
{
    public TrainingYoloExportDependencies(ITrainingYoloExportCoordinator coordinator)
    {
        Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public ITrainingYoloExportCoordinator Coordinator { get; }
}
