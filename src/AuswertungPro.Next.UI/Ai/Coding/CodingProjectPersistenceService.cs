using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingProjectPersistenceService
{
    private readonly Func<HaltungRecord?, bool> _markProjectDirty;
    private readonly Action _trySaveProjectIfReady;
    private readonly Func<DateTime> _utcNow;

    public CodingProjectPersistenceService(
        Func<HaltungRecord?, bool> markProjectDirty,
        Action trySaveProjectIfReady,
        Func<DateTime> utcNow)
    {
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _trySaveProjectIfReady = trySaveProjectIfReady ?? throw new ArgumentNullException(nameof(trySaveProjectIfReady));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public void MarkProjectDirty(HaltungRecord? record)
    {
        if (_markProjectDirty(record))
            return;

        if (record is not null)
            record.ModifiedAtUtc = _utcNow();
    }

    public void TrySaveProjectIfReady()
        => _trySaveProjectIfReady();
}
