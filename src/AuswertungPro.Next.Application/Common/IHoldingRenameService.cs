using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Benennt eine Haltung samt Dateien und gespeicherten Pfaden sicher um.</summary>
public interface IHoldingRenameService
{
    HoldingRenameService.HoldingRenameResult Rename(
        HaltungRecord record,
        string oldHolding,
        string newHolding,
        string? projectFilePath);
}
