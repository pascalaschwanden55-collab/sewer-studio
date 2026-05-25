namespace AuswertungPro.Next.Application.Ai.Training;

public sealed record TrainingCaseInput(
    string CaseId,
    string FolderPath,
    string VideoPath,
    string ProtocolPath);
