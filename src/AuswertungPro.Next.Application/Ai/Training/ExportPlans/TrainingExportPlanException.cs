namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public sealed class TrainingExportPlanException : InvalidOperationException
{
    public TrainingExportPlanException(string message)
        : base(message)
    {
    }

    public TrainingExportPlanException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
