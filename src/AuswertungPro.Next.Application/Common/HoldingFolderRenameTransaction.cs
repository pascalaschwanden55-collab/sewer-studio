using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Benennt einen projektinternen Haltungsordner samt Dateien und Unterordnern um.
/// Jeder einzelne Schritt wird fuer einen moeglichen Rollback festgehalten.
/// </summary>
internal sealed class HoldingFolderRenameTransaction
{
    private static readonly EnumerationOptions RecursiveEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private readonly List<RenameMove> _appliedMoves;

    private HoldingFolderRenameTransaction(
        bool success,
        string? errorMessage,
        List<RenameMove>? appliedMoves = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
        _appliedMoves = appliedMoves ?? [];
    }

    internal bool Success { get; }
    internal string? ErrorMessage { get; }
    internal bool FolderRenamed => Success && _appliedMoves.Count > 0;

    internal static HoldingFolderRenameTransaction Execute(
        string sourceFolder,
        string targetFolder,
        IReadOnlyCollection<string> oldAliases,
        string newHolding)
    {
        try
        {
            var plans = BuildPlans(sourceFolder, targetFolder, oldAliases, newHolding);
            ValidatePlans(plans);

            var applied = new List<RenameMove>(plans.Count);
            try
            {
                foreach (var plan in plans)
                {
                    if (plan.IsDirectory)
                        Directory.Move(plan.Source, plan.Destination);
                    else
                        File.Move(plan.Source, plan.Destination);

                    applied.Add(plan);
                }

                return new HoldingFolderRenameTransaction(true, null, applied);
            }
            catch (Exception ex)
            {
                var rollbackError = RollbackMoves(applied);
                var message = rollbackError is null
                    ? ex.Message
                    : $"{ex.Message} Rollback fehlgeschlagen: {rollbackError}";
                return new HoldingFolderRenameTransaction(false, message);
            }
        }
        catch (Exception ex)
        {
            return new HoldingFolderRenameTransaction(false, ex.Message);
        }
    }

    /// <summary>Rollt eine bereits erfolgreich abgeschlossene Ordnerumbenennung zurueck.</summary>
    internal string? Rollback()
    {
        var error = RollbackMoves(_appliedMoves);
        if (error is null)
            _appliedMoves.Clear();
        return error;
    }

    /// <summary>
    /// Erkennt die Nummer aus den von SewerStudio erzeugten Namen
    /// JJJJMMTT_HALTUNG, JJJJMMTT-HALTUNG sowie den Suffixen _E, _G und -g.
    /// So koennen auch alte, bereits abweichende Dateinamen korrigiert werden.
    /// </summary>
    internal static IReadOnlyCollection<string> CollectDatePrefixedHoldingAliases(string folder)
    {
        if (!Directory.Exists(folder))
            return [];

        return Directory
            .EnumerateFiles(folder, "*", RecursiveEnumeration)
            .Select(path => TryExtractDatePrefixedHolding(Path.GetFileNameWithoutExtension(path)))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<RenameMove> BuildPlans(
        string sourceFolder,
        string targetFolder,
        IReadOnlyCollection<string> oldAliases,
        string newHolding)
    {
        var aliases = oldAliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .OrderByDescending(alias => alias.Length)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var plans = new List<RenameMove>();

        foreach (var source in Directory.EnumerateFiles(sourceFolder, "*", RecursiveEnumeration))
        {
            var newName = ReplaceAliases(Path.GetFileName(source), aliases, newHolding);
            var destination = Path.Combine(Path.GetDirectoryName(source)!, newName);
            if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                plans.Add(new RenameMove(source, destination, IsDirectory: false));
        }

        // Tiefste Unterordner zuerst. Beim Rollback laeuft die Reihenfolge umgekehrt,
        // damit Elternordner vor ihren Kindern wieder am alten Platz stehen.
        foreach (var source in Directory
                     .EnumerateDirectories(sourceFolder, "*", RecursiveEnumeration)
                     .OrderByDescending(GetPathDepth))
        {
            var newName = ReplaceAliases(Path.GetFileName(source), aliases, newHolding);
            var destination = Path.Combine(Path.GetDirectoryName(source)!, newName);
            if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                plans.Add(new RenameMove(source, destination, IsDirectory: true));
        }

        if (!string.Equals(sourceFolder, targetFolder, StringComparison.OrdinalIgnoreCase))
            plans.Add(new RenameMove(sourceFolder, targetFolder, IsDirectory: true));

        return plans;
    }

    private static void ValidatePlans(IReadOnlyCollection<RenameMove> plans)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in plans)
        {
            if (!targets.Add(Path.GetFullPath(plan.Destination)))
                throw new IOException($"Mehrere Dateien oder Ordner haben denselben Zielnamen: {plan.Destination}");

            if (File.Exists(plan.Destination) || Directory.Exists(plan.Destination))
                throw new IOException($"Ziel existiert bereits: {plan.Destination}");
        }
    }

    private static string? RollbackMoves(IReadOnlyList<RenameMove> moves)
    {
        List<string>? errors = null;
        for (var i = moves.Count - 1; i >= 0; i--)
        {
            var move = moves[i];
            try
            {
                if (move.IsDirectory)
                {
                    if (Directory.Exists(move.Destination) && !Directory.Exists(move.Source))
                        Directory.Move(move.Destination, move.Source);
                }
                else if (File.Exists(move.Destination) && !File.Exists(move.Source))
                {
                    File.Move(move.Destination, move.Source);
                }
            }
            catch (Exception ex)
            {
                errors ??= [];
                errors.Add($"{move.Destination}: {ex.Message}");
            }
        }

        return errors is { Count: > 0 } ? string.Join(" | ", errors) : null;
    }

    private static string ReplaceAliases(
        string name,
        IReadOnlyCollection<string> aliases,
        string newHolding)
    {
        var result = name;
        foreach (var alias in aliases)
            result = result.Replace(alias, newHolding, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static string? TryExtractDatePrefixedHolding(string? stem)
    {
        if (string.IsNullOrWhiteSpace(stem)
            || stem.Length < 10
            || !stem[..8].All(char.IsDigit)
            || stem[8] is not ('_' or '-'))
        {
            return null;
        }

        var candidate = stem[9..];
        foreach (var suffix in new[] { "_E", "_G", "-g" })
        {
            if (candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                candidate = candidate[..^suffix.Length];
        }

        var endpoints = candidate.Split(
            '-',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (endpoints.Length != 2 || endpoints.Any(endpoint =>
                !endpoint.Any(char.IsDigit)
                || endpoint.Any(character => !char.IsDigit(character) && character != '.')))
        {
            return null;
        }

        return candidate;
    }

    private static int GetPathDepth(string path)
        => path.Count(character =>
            character == Path.DirectorySeparatorChar
            || character == Path.AltDirectorySeparatorChar);

    private sealed record RenameMove(string Source, string Destination, bool IsDirectory);
}
