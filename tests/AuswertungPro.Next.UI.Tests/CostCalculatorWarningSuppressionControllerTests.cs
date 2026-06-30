using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorWarningSuppressionControllerTests
{
    [Fact]
    public void FilterVisibleWarnings_blendet_nur_gleichen_warning_key_aus()
    {
        var controller = new CostCalculatorWarningSuppressionController();
        var suppressed = Warning("RULE-1", "M-1", "A.1");
        var sameKey = Warning("RULE-1", "M-1", "A.1");
        var differentItemKey = Warning("RULE-1", "M-1", "A.2");

        controller.SuppressWarning(suppressed);

        var visible = controller.FilterVisibleWarnings([sameKey, differentItemKey]);

        Assert.Equal([differentItemKey], visible);
    }

    [Fact]
    public void ResetSuppressedWarnings_macht_ausgeblendete_warnung_wieder_sichtbar()
    {
        var controller = new CostCalculatorWarningSuppressionController();
        var warning = Warning("RULE-1", "M-1", "A.1");
        controller.SuppressWarning(warning);

        controller.ResetSuppressedWarnings();

        Assert.Equal([warning], controller.FilterVisibleWarnings([warning]));
    }

    private static ConsistencyWarning Warning(string ruleId, string measureId, string itemKey)
        => new()
        {
            RuleId = ruleId,
            Severity = ConsistencyWarningSeverity.Warning,
            Message = "Warnung",
            MeasureId = measureId,
            ItemKey = itemKey
        };
}
