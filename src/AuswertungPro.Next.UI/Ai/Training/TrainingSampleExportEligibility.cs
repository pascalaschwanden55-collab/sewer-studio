using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingSampleExportEligibility
{
    public static bool EvaluateAndUpdate(TrainingSample sample, ICodeCatalogProvider? codeCatalog)
    {
        var result = codeCatalog is null
            ? TrainingSampleEligibility.Evaluate(sample)
            : TrainingSampleEligibility.Evaluate(sample, codeCatalog);

        sample.TrainingEligible = result.IsEligible;
        sample.TrainingEligibilityReason = result.Reason;
        return result.IsEligible;
    }
}
