using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public interface IPlayerShellProjectContext
{
    Project Project { get; }
    bool IsProjectReady { get; }
    void MarkProjectDirty(HaltungRecord? record = null);
    bool TrySaveProject();
}

public sealed class PlayerShellProjectService
{
    private readonly Func<object?> _dataContextProvider;

    public PlayerShellProjectService(Func<object?> dataContextProvider)
    {
        _dataContextProvider = dataContextProvider ?? throw new ArgumentNullException(nameof(dataContextProvider));
    }

    public Project? GetCurrentProject()
        => GetContext()?.Project;

    public bool MarkProjectDirty(HaltungRecord? record)
    {
        var context = GetContext();
        if (context == null)
            return false;

        context.MarkProjectDirty(record);
        return true;
    }

    public bool TrySaveProjectIfReady()
    {
        var context = GetContext();
        return context is { IsProjectReady: true } && context.TrySaveProject();
    }

    private IPlayerShellProjectContext? GetContext()
        => _dataContextProvider() as IPlayerShellProjectContext;
}
