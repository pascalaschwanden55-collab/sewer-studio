namespace AuswertungPro.Next.UI;

public enum AutoSaveMode
{
    OnEachChange = 0,
    Every5Minutes = 1,
    Every10Minutes = 2,
    Disabled = 3
}

public static class AutoSaveModeExtensions
{
    public static AutoSaveMode Normalize(this AutoSaveMode mode)
        => mode switch
        {
            AutoSaveMode.OnEachChange => AutoSaveMode.OnEachChange,
            AutoSaveMode.Every5Minutes => AutoSaveMode.Every5Minutes,
            AutoSaveMode.Every10Minutes => AutoSaveMode.Every10Minutes,
            AutoSaveMode.Disabled => AutoSaveMode.Disabled,
            _ => AutoSaveMode.OnEachChange
        };

    public static TimeSpan? GetInterval(this AutoSaveMode mode)
        => mode.Normalize() switch
        {
            AutoSaveMode.Every5Minutes => TimeSpan.FromMinutes(5),
            AutoSaveMode.Every10Minutes => TimeSpan.FromMinutes(10),
            _ => null
        };
}
