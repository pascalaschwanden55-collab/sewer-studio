using System;
using System.IO;
using InfraKnowledgeBase = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Alle Speicherorte, die zu einer KI-Wissenssicherung gehoeren.
/// Die gebuendelte Uebergabe verhindert, dass Tests oder spaetere Dienste
/// versehentlich auf echte Benutzerdaten zugreifen.
/// </summary>
internal sealed record KnowledgeBackupLocations(
    string KnowledgeRoot,
    string RoamingAuswertungPro,
    string RoamingSewerStudio,
    string LocalSewerStudio,
    string TrainingCenterStatePath,
    string TempRoot)
{
    public static KnowledgeBackupLocations FromCurrentSystem()
        => new(
            KnowledgeRoot: InfraKnowledgeBase.KnowledgeBasePaths.GetRoot(),
            RoamingAuswertungPro: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AuswertungPro"),
            RoamingSewerStudio: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppIdentity.ProductName),
            LocalSewerStudio: AppSettings.AppDataDir,
            TrainingCenterStatePath: new Ai.Training.TrainingCenterStore().StoreFilePath,
            TempRoot: Path.GetTempPath());
}
