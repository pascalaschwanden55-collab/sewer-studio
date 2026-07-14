using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Benennt einen Schacht samt Dateien und gespeicherten Pfaden sicher um.</summary>
public interface IShaftRenameService
{
    ShaftRenameService.ShaftRenameResult Rename(
        SchachtRecord record,
        string oldShaftNumber,
        string newShaftNumber,
        string? projectFilePath);
}
