namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

/// <summary>Liest das freizugebende Haltungs- und Schutz-Set-Register strikt.</summary>
public interface ITrainingExportRegistryStore
{
    TrainingExportRegistryBundle ReadBundle();
}
