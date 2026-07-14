namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Legt vor dem Ueberschreiben der Programmeinstellungen eine begrenzte Sicherungskopie an.
/// Fehler bleiben best effort und duerfen den eigentlichen Speichervorgang nicht abbrechen.
/// </summary>
public interface ISettingsRestorePointStore
{
    void TryCreate(string sourceFilePath, string restoreRoot, string scopeName);
}
