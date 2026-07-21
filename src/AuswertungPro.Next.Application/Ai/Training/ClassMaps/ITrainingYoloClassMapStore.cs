namespace AuswertungPro.Next.Application.Ai.Training.ClassMaps;

/// <summary>
/// Liefert einen unveraenderlichen, geprueften Klassenstand fuer genau einen Exportlauf.
/// </summary>
public interface ITrainingYoloClassMapStore
{
    TrainingYoloClassMapSnapshot ReadSnapshot();
}
