using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using AuswertungPro.Next.UI.Hydraulik;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class HydraulikPanelViewModel : ObservableObject
{
    private const double MinTemperaturC = 0;
    private const double MaxTemperaturC = 40;
    private bool _suppressSave;

    // ── Material → kb Zuordnung (VSA-typische Werte) ──────────
    public static IReadOnlyList<MaterialOption> Materialien { get; } = new[]
    {
        new MaterialOption("Beton", "Beton", 0.0005, 0.0015),
        new MaterialOption("Steinzeug", "Steinzeug", 0.0003, 0.001),
        new MaterialOption("PVC/PE", "Kunststoff (PVC/PE)", 0.0002, 0.0005),
        new MaterialOption("GFK", "GFK", 0.0003, 0.0008),
        new MaterialOption("Guss", "Gusseisen", 0.001, 0.003),
    };

    // ── Eingabewerte ──────────────────────────────────────────
    [ObservableProperty] private double _dn = 300;
    [ObservableProperty] private MaterialOption _selectedMaterial;
    [ObservableProperty] private bool _isNeuzustand;
    [ObservableProperty] private double _gefaelle = 5;
    [ObservableProperty] private bool _isGefaellePercent;
    [ObservableProperty] private double _wasserstand = 90;
    [ObservableProperty] private bool _isMischRegen = true;
    [ObservableProperty] private double _temperatur = 10;

    // ── Berechnete Ergebnisse ─────────────────────────────────
    [ObservableProperty] private HydraulikResult? _result;
    [ObservableProperty] private bool _hasResult;

    // Formatted result strings for binding
    [ObservableProperty] private string _vTeilDisplay = "—";
    [ObservableProperty] private string _qTeilDisplay = "—";
    [ObservableProperty] private string _aTeilDisplay = "—";
    [ObservableProperty] private string _luTeilDisplay = "—";
    [ObservableProperty] private string _rhyTeilDisplay = "—";
    [ObservableProperty] private string _bspDisplay = "—";

    [ObservableProperty] private string _vVollDisplay = "—";
    [ObservableProperty] private string _qVollDisplay = "—";

    [ObservableProperty] private string _reDisplay = "—";
    [ObservableProperty] private string _frDisplay = "—";
    [ObservableProperty] private string _lambdaDisplay = "—";
    [ObservableProperty] private string _kbDisplay = "—";
    [ObservableProperty] private string _nyDisplay = "—";
    [ObservableProperty] private string _tauDisplay = "—";

    [ObservableProperty] private string _vcDisplay = "—";
    [ObservableProperty] private string _icDisplay = "—";
    [ObservableProperty] private string _taucDisplay = "—";
    [ObservableProperty] private string _ablagerungText = "";

    [ObservableProperty] private string _auslastungDisplay = "—";
    [ObservableProperty] private double _auslastungPercent;

    [ObservableProperty] private bool _velocityOk;
    [ObservableProperty] private bool _shearOk;
    [ObservableProperty] private bool _ablagerungOk;
    [ObservableProperty] private bool _froudeOk;

    public string AbwasserTypLabel => IsMischRegen ? "Misch-/Regenwasser" : "Schmutzwasser";
    public string ZustandLabel => IsNeuzustand ? "Neuzustand" : "Betriebszustand";
    public string GefaelleUnitLabel => IsGefaellePercent ? "%" : "‰";

    /// <summary>Gefaelle immer in Promille, unabhaengig von der Einheit-Auswahl.</summary>
    public double GefaellePromille => IsGefaellePercent ? Gefaelle * 10 : Gefaelle;

    public HydraulikPanelViewModel()
    {
        _selectedMaterial = Materialien[0];
        LoadFromSettings();
        Recalculate();
    }

    /// <summary>Initialisiert das Panel mit Werten aus einer Haltung.</summary>
    public void LoadFromRecord(double? dn, string? material, double? wasserstand)
    {
        if (dn.HasValue && dn.Value > 0) Dn = dn.Value;
        if (wasserstand.HasValue && wasserstand.Value > 0) Wasserstand = wasserstand.Value;

        if (!string.IsNullOrWhiteSpace(material))
        {
            foreach (var mat in Materialien)
            {
                if (mat.Label.Contains(material, StringComparison.OrdinalIgnoreCase)
                    || mat.Key.Equals(material, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedMaterial = mat;
                    break;
                }
            }
        }
    }

    partial void OnDnChanged(double value) => Recalculate();
    partial void OnSelectedMaterialChanged(MaterialOption value) => Recalculate();
    partial void OnIsNeuzustandChanged(bool value)
    {
        OnPropertyChanged(nameof(ZustandLabel));
        Recalculate();
    }
    partial void OnGefaelleChanged(double value) => Recalculate();
    partial void OnIsGefaellePercentChanged(bool value)
    {
        // Convert the displayed value to the new unit
        // ‰ → %: divide by 10 | % → ‰: multiply by 10
        Gefaelle = value ? Gefaelle / 10.0 : Gefaelle * 10.0;
        OnPropertyChanged(nameof(GefaelleUnitLabel));
        // Recalculate is triggered by OnGefaelleChanged
    }
    partial void OnWasserstandChanged(double value) => Recalculate();
    partial void OnIsMischRegenChanged(bool value)
    {
        OnPropertyChanged(nameof(AbwasserTypLabel));
        Recalculate();
    }
    partial void OnTemperaturChanged(double value)
    {
        var clamped = Math.Clamp(value, MinTemperaturC, MaxTemperaturC);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            Temperatur = clamped;
            return;
        }

        Recalculate();
    }

    private void Recalculate()
    {
        var mat = SelectedMaterial;
        if (mat is null) return;

        double kb = IsNeuzustand ? mat.KbNeu : mat.KbAlt;

        var input = new HydraulikInput(
            DN_mm: Dn,
            Wasserstand_mm: Math.Min(Wasserstand, Dn),
            Gefaelle_Promille: GefaellePromille,
            Kb: kb,
            AbwasserTyp: IsMischRegen ? "MR" : "S",
            Temperatur_C: Temperatur);

        var r = HydraulikEngine.Berechne(input);
        Result = r;
        HasResult = r is not null;

        if (r is null)
        {
            ClearDisplayValues();
            return;
        }

        // Teilfüllung
        VTeilDisplay = Fmt(r.V_T, 3);
        QTeilDisplay = Fmt(r.Q_T * 1000, 2);
        ATeilDisplay = Fmt(r.A_T * 1e4, 2);
        LuTeilDisplay = Fmt(r.Lu_T * 1000, 1);
        RhyTeilDisplay = Fmt(r.Rhy_T * 1000, 2);
        BspDisplay = Fmt(r.Bsp * 1000, 1);

        // Vollfüllung
        VVollDisplay = Fmt(r.V_V, 3);
        QVollDisplay = Fmt(r.Q_V * 1000, 2);

        // Kennzahlen
        ReDisplay = Fmt(r.Re, 0);
        FrDisplay = Fmt(r.Fr, 3);
        LambdaDisplay = FmtSci(r.Lambda);
        KbDisplay = Fmt(r.Kb * 1000, 2);
        NyDisplay = (r.Ny * 1e6).ToString("F3", CultureInfo.InvariantCulture);
        TauDisplay = Fmt(r.Tau, 2);

        // Ablagerung
        VcDisplay = Fmt(r.Abl.Vc, 3);
        IcDisplay = Fmt(r.Abl.Ic * 1000, 2);
        TaucDisplay = Fmt(r.Abl.TauC, 2);

        AblagerungOk = r.AblagerungOk;
        AblagerungText = r.AblagerungOk
            ? $"Ablagerungsfrei — v_T ({Fmt(r.V_T, 3)} m/s) >= v_c ({Fmt(r.Abl.Vc, 3)} m/s)"
            : $"Ablagerungsgefahr — v_T ({Fmt(r.V_T, 3)} m/s) < v_c ({Fmt(r.Abl.Vc, 3)} m/s)";

        // Auslastung
        AuslastungPercent = r.Auslastung * 100;
        AuslastungDisplay = AuslastungPercent.ToString("F0", CultureInfo.InvariantCulture) + "%";

        // Bewertungen
        VelocityOk = r.VelocityOk;
        ShearOk = r.ShearOk;
        FroudeOk = r.Fr <= 1;

        SaveToSettings();
    }

    private void ClearDisplayValues()
    {
        VTeilDisplay = QTeilDisplay = ATeilDisplay = LuTeilDisplay = "—";
        RhyTeilDisplay = BspDisplay = VVollDisplay = QVollDisplay = "—";
        ReDisplay = FrDisplay = LambdaDisplay = KbDisplay = NyDisplay = TauDisplay = "—";
        VcDisplay = IcDisplay = TaucDisplay = "—";
        AblagerungText = "";
        AuslastungDisplay = "—";
        AuslastungPercent = 0;
        VelocityOk = ShearOk = AblagerungOk = FroudeOk = false;
    }

    #pragma warning disable MVVMTK0034
    private void LoadFromSettings()
    {
        try
        {
            _suppressSave = true;
            var s = GetWritableSettings().HydraulikPanel;
            if (s is null) return;

            _dn = s.Dn;
            _gefaelle = s.Gefaelle;
            _isGefaellePercent = s.IsGefaellePercent;
            _wasserstand = s.Wasserstand;
            _isNeuzustand = s.IsNeuzustand;
            _isMischRegen = s.IsMischRegen;
            _temperatur = s.Temperatur;

            var mat = Materialien.FirstOrDefault(m =>
                string.Equals(m.Key, s.MaterialKey, StringComparison.OrdinalIgnoreCase));
            if (mat is not null)
                _selectedMaterial = mat;
        }
        catch
        {
            // ignore corrupt settings
        }
        finally
        {
            _suppressSave = false;
        }
    }

    #pragma warning restore MVVMTK0034

    private void SaveToSettings()
    {
        if (_suppressSave) return;

        try
        {
            var settings = GetWritableSettings();
            settings.HydraulikPanel ??= new HydraulikPanelSettings();
            var s = settings.HydraulikPanel;
            s.Dn = Dn;
            s.MaterialKey = SelectedMaterial?.Key ?? "Beton";
            s.IsNeuzustand = IsNeuzustand;
            s.Gefaelle = Gefaelle;
            s.IsGefaellePercent = IsGefaellePercent;
            s.Wasserstand = Wasserstand;
            s.IsMischRegen = IsMischRegen;
            s.Temperatur = Temperatur;
            settings.Save();
        }
        catch
        {
            // ignore save errors
        }
    }

    private static AppSettings GetWritableSettings()
        => (App.Services as ServiceProvider)?.Settings ?? AppSettings.Load();

    private static string Fmt(double v, int dec) =>
        v.ToString($"F{dec}", CultureInfo.InvariantCulture);

    private static string FmtSci(double v)
    {
        if (v == 0) return "—";
        if (v < 0.001) return v.ToString("E2", CultureInfo.InvariantCulture);
        return v.ToString("F4", CultureInfo.InvariantCulture);
    }
}

public sealed record MaterialOption(string Key, string Label, double KbNeu, double KbAlt)
{
    public override string ToString() => Label;
}
