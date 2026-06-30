using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class CostCalculatorImportDefaultsController
{
    private string? _defaultDn;
    private string? _defaultLength;
    private string? _defaultConnections;

    public void InitializeFromHaltungRecord(
        HaltungRecord haltungRecord,
        IEnumerable<MeasureBlockVm> measures)
    {
        ArgumentNullException.ThrowIfNull(haltungRecord);
        ArgumentNullException.ThrowIfNull(measures);

        var defaults = MeasureImportDefaultsResolver.Resolve(haltungRecord);
        var measureList = measures.ToList();

        if (defaults.Dn.HasValue)
        {
            _defaultDn = defaults.Dn.Value.ToString(CultureInfo.InvariantCulture);
            foreach (var measure in measureList)
                measure.SetDnFromImport(_defaultDn);
        }

        if (defaults.LengthMeters.HasValue)
        {
            _defaultLength = defaults.LengthMeters.Value.ToString("0.00", CultureInfo.InvariantCulture);
            foreach (var measure in measureList)
                measure.SetLengthFromImport(_defaultLength);
        }

        // Anschlussanzahl: 0 ist explizit und deaktiviert Anschluss-Zeilen.
        _defaultConnections = defaults.Connections.ToString(CultureInfo.InvariantCulture);
        foreach (var measure in measureList)
            measure.SetConnectionsFromImport(_defaultConnections);
    }

    public void ApplyTo(MeasureBlockVm block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (!string.IsNullOrWhiteSpace(_defaultDn))
            block.SetDnFromImport(_defaultDn);
        if (!string.IsNullOrWhiteSpace(_defaultLength))
            block.SetLengthFromImport(_defaultLength);
        if (!string.IsNullOrWhiteSpace(_defaultConnections))
            block.SetConnectionsFromImport(_defaultConnections);
    }
}
