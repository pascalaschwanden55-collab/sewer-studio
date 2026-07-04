using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCaseInputMapper
{
    public static TrainingCase ToTrainingCase(TrainingCaseInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new TrainingCase
        {
            CaseId = input.CaseId,
            FolderPath = input.FolderPath,
            VideoPath = input.VideoPath,
            ProtocolPath = input.ProtocolPath,
            InspectionDate = input.InspectionDate,
            Status = TrainingCaseStatus.New,
            CreatedUtc = DateTime.UtcNow
        };
    }
}
