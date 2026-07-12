namespace AuswertungPro.Next.UI.Settings;

public sealed record FullBackupReminder(bool ShouldRemind, string? Message, bool IsOverdue)
{
    public static readonly FullBackupReminder None = new(false, null, false);
}

public static class FullBackupReminderPolicy
{
    public static FullBackupReminder Evaluate(DateTime? lastBackupUtc, DateTime utcNow)
    {
        if (lastBackupUtc is null)
        {
            return new(true,
                "Es wurde noch keine komplette Datensicherung erstellt. Bitte richte den PC-Ausfallsschutz im Tab Datensicherung ein.",
                true);
        }

        var age = utcNow - DateTime.SpecifyKind(lastBackupUtc.Value, DateTimeKind.Utc);
        if (age < TimeSpan.FromDays(7))
            return FullBackupReminder.None;

        var days = Math.Max(7, (int)Math.Floor(age.TotalDays));
        return age >= TimeSpan.FromDays(30)
            ? new(true, $"Die letzte komplette Datensicherung ist {days} Tage alt. Bitte jetzt eine neue Sicherung erstellen.", true)
            : new(true, $"Die letzte komplette Datensicherung ist {days} Tage alt. Eine neue Sicherung wird empfohlen.", false);
    }
}
