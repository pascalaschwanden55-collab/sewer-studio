namespace AuswertungPro.Next.Application.Ai.Training.ClassMaps;

public sealed class TrainingYoloClassMapException : InvalidOperationException
{
    public TrainingYoloClassMapException(string message)
        : base(message)
    {
    }

    public TrainingYoloClassMapException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
