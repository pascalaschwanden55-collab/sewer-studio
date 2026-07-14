namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Laedt und speichert die Einstellungen des Training Centers unabhaengig von der Oberfläche.
/// </summary>
public interface ITrainingCenterSettingsStore
{
    string StoragePath { get; }

    Task<TrainingCenterSettings> LoadAsync();

    Task SaveAsync(TrainingCenterSettings settings);
}
