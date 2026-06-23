using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingApplyEmptyProtocolGuardTests
{
    [Fact]
    public void Build_does_not_require_confirmation_when_new_coding_has_events()
    {
        var result = CodingApplyEmptyProtocolGuard.Build(
            codingEventEntryCount: 1,
            existingEntries: [new ProtocolEntry { Code = "BAA" }]);

        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Build_does_not_require_confirmation_when_existing_protocol_has_no_active_findings()
    {
        var result = CodingApplyEmptyProtocolGuard.Build(
            codingEventEntryCount: 0,
            existingEntries:
            [
                new ProtocolEntry { Code = "" },
                new ProtocolEntry { Code = "BAA", IsDeleted = true }
            ]);

        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Build_requires_confirmation_when_empty_coding_would_clear_active_findings()
    {
        var result = CodingApplyEmptyProtocolGuard.Build(
            codingEventEntryCount: 0,
            existingEntries:
            [
                new ProtocolEntry { Code = "BAA" },
                new ProtocolEntry { Code = "BCA" },
                new ProtocolEntry { Code = "IGNORED", IsDeleted = true }
            ]);

        Assert.True(result.RequiresConfirmation);
        Assert.Contains("2", result.Message);
        Assert.Equal("Leere Codierung \u00fcbernehmen?", result.Title);
    }
}
