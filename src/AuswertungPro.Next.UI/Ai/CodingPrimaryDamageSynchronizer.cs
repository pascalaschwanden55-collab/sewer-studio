using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingPrimaryDamageSynchronizer
{
    private readonly Func<ProtocolDocument, string> _buildText;
    private readonly Func<DateTime> _utcNow;

    public CodingPrimaryDamageSynchronizer(
        Func<ProtocolDocument, string> buildText,
        Func<DateTime> utcNow)
    {
        _buildText = buildText ?? throw new ArgumentNullException(nameof(buildText));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public void Sync(HaltungRecord record, ProtocolDocument doc)
    {
        var primaryText = _buildText(doc);
        record.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
        record.ModifiedAtUtc = _utcNow();
    }
}
