using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3: Optionaler Mirror-Notifier — UI registriert beim Start einen Callback,
/// der nach Schreibzugriffen auf KnowledgeRoot-Dateien den E:\Brain Mirror anstoesst.
/// Wird kein Notifier registriert, ist NotifyChanged() ein No-op.
/// </summary>
public static class KnowledgeMirrorNotifier
{
    private static Action? _notify;

    /// <summary>Registriert den Notifier-Callback. UI: NotifyChanged von KnowledgeMirrorService.Current.</summary>
    public static void SetNotifier(Action notify)
        => _notify = notify ?? throw new ArgumentNullException(nameof(notify));

    /// <summary>Loest einen Mirror-Sync aus. No-op wenn kein Notifier registriert.</summary>
    public static void NotifyChanged() => _notify?.Invoke();
}
