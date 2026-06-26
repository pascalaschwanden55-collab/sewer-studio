using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerWindowProtocolContext
{
    private PlayerWindowProtocolContext(
        PlayerWindowDependencies dependencies,
        string? haltungId,
        Action<ProtocolEntry>? onEntryCreated,
        HaltungRecord? haltungRecord)
    {
        Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        HaltungId = haltungId;
        _onEntryCreated = onEntryCreated;
        HaltungRecord = haltungRecord;
    }

    private readonly Action<ProtocolEntry>? _onEntryCreated;

    public PlayerWindowDependencies Dependencies { get; }

    public string? HaltungId { get; }

    public HaltungRecord? HaltungRecord { get; }

    public bool HasHaltungRecord => HaltungRecord is not null;

    public static PlayerWindowProtocolContext From(
        ServiceProvider? serviceProvider,
        string? haltungId,
        Action<ProtocolEntry>? onEntryCreated,
        HaltungRecord? haltungRecord)
        => new(
            PlayerWindowDependencies.From(serviceProvider),
            haltungId,
            onEntryCreated,
            haltungRecord);

    public void NotifyEntryCreated(ProtocolEntry entry)
    {
        _onEntryCreated?.Invoke(entry);
    }
}
