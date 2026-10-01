using System;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Loest den Pfad zur Abwasserkataster-XTF aus den Einstellungen auf.
///
/// Namensvermerk: Diese Klasse ist der einzige verbliebene Inhalt des Ordners
/// "Mapping". Die Kartenansicht wurde am 2026-08-30 entfernt; verwendet wird sie
/// heute von der QGIS-Bruecke, dem Einstellungs-Speicherweg und der Exportseite.
/// Sie darf spaeter in einen passender benannten Ordner verschoben werden.
/// </summary>
public static class KatasterXtfPathResolver
{
    private static readonly IKatasterXtfPathResolver Default = new KatasterXtfFilePathResolver();

    internal static IKatasterXtfPathResolver CompatibilityService
        => Default;

    public static string Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return CompatibilityService.Resolve(
            settings.AbwasserkatasterXtfPath,
            settings.KantonUriXtfDirectory);
    }

    public static string Resolve(string? explicitPath, string? directoryPath)
    {
        return CompatibilityService.Resolve(explicitPath, directoryPath);
    }

    public static string? TryFindKatasterXtf(string? directoryPath)
    {
        return CompatibilityService.TryFindKatasterXtf(directoryPath);
    }
}
