using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FullBackupReminderPolicyTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_NoBackup_RemindsImmediately()
    {
        var result = FullBackupReminderPolicy.Evaluate(null, Now);
        Assert.True(result.ShouldRemind);
        Assert.True(result.IsOverdue);
        Assert.Contains("noch keine", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(6, false, false)]
    [InlineData(7, true, false)]
    [InlineData(29, true, false)]
    [InlineData(30, true, true)]
    public void Evaluate_UsesSevenAndThirtyDayThresholds(int days, bool remind, bool overdue)
    {
        var result = FullBackupReminderPolicy.Evaluate(Now.AddDays(-days), Now);
        Assert.Equal(remind, result.ShouldRemind);
        Assert.Equal(overdue, result.IsOverdue);
    }
}
