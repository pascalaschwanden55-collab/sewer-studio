using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.Backup;
using InfraKnowledgeBase = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Ermittelt die rechnerabhaengigen Standardpfade. Die ZIP-Dateiarbeit selbst
/// liegt in der Infrastructure.
/// </summary>
internal static class KnowledgeBackupLocationFactory
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
