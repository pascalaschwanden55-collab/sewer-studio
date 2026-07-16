namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>Liefert die bekannten Fall-Ids fuer Markierungen ausserhalb des Training Centers.</summary>
public interface ITrainingCaseIdSource
{
    Task<IReadOnlyList<string>> LoadCaseIdsAsync(CancellationToken cancellationToken = default);
}
