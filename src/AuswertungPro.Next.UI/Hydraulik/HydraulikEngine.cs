using System;

namespace AuswertungPro.Next.UI.Hydraulik;

/// <summary>
/// Hydraulik-Berechnungsengine nach DWA-A 110 / HydroDim.
/// Kreisquerschnitt, Normalabfluss bei Teilfüllung.
/// </summary>
public static class HydraulikEngine
{
    private const double G = 9.81;
    private const double G2 = 2 * G; // 19.62

    // Ablagerungsgefahr nach Macke (1982)
    private const double WC = 0.057; // Sinkgeschwindigkeit [m/s]
    private const double CT_MR = 0.05 / 1000;
    private const double CT_S = 0.03 / 1000;

    private static double Lg(double x) =>
        x <= 0 ? 0 : Math.Log10(x);

    /// <summary>Reynolds-Zahl.</summary>
    public static double ReynoldsZahl(double v, double rhy, double ny) =>
        ny == 0 ? 0 : v * 4 * rhy / ny;

    /// <summary>Froude-Zahl.</summary>
    public static double FroudeZahl(double v, double A, double bsp)
    {
        if (A == 0 || bsp == 0) return 0;
        var depth = A / bsp;
        return depth < 0 ? 0 : v / Math.Sqrt(G * depth);
    }

    /// <summary>Widerstandsbeiwert λ (Colebrook-White iterativ).</summary>
    public static double Lambda(double kb, double rhy, double Re)
    {
        if (kb <= 0 || rhy <= 0 || Re <= 0) return 0;

        double laNeu = Math.Pow(1.0 / (1.14 - 2 * Lg(kb / (4 * rhy))), 2);
        double la;
        int iter = 0;
        do
        {
            la = laNeu;
            laNeu = 1.0 / Math.Pow(
                -2.0 * Lg(2.51 / (Re * Math.Sqrt(la)) + kb / (3.71 * 4 * rhy)), 2);
            iter++;
        } while (Math.Abs(la - laNeu) > 1e-8 && iter < 100);

        return laNeu;
    }

    /// <summary>Fliessgeschwindigkeit nach Prandtl-Colebrook [m/s].</summary>
    public static double Geschwindigkeit(double IE, double rhy, double kb, double ny)
    {
        if (IE == 0 || rhy <= 0 || kb == 0 || ny == 0) return 0;

        double v = -2.0 * Lg(
                (2.51 * ny) / (4 * rhy * Math.Sqrt(G2 * 4 * rhy * IE))
                + kb / (14.84 * rhy))
            * Math.Sqrt(G2 * 4 * rhy * IE);

        return v > 0.01 ? v : 0;
    }

    /// <summary>Energiehöhengefälle I_E.</summary>
    public static double Energiehoehengefaelle(double lambda, double rhy, double v) =>
        rhy == 0 ? 0 : lambda * (1.0 / (4.0 * rhy)) * (v * v / G2);

    /// <summary>Sohlschubspannung τ [N/m²].</summary>
    public static double Schubspannung(double rho, double rhy, double IE) =>
        rho * G * rhy * IE;

    // ── Kreisprofil-Geometrie ────────────────────────────────────

    /// <summary>Durchflossene Fläche bei Teilfüllung [m²].</summary>
    public static double KreisTeilflaeche(double d, double h)
    {
        if (h <= 0 || d <= 0) return 0;
        if (h >= d) return Math.PI * d * d / 4;
        double r = d / 2;
        double alpha = 2 * Math.Acos((r - h) / r);
        return r * r * (alpha - Math.Sin(alpha)) / 2;
    }

    /// <summary>Benetzter Umfang bei Teilfüllung [m].</summary>
    public static double KreisTeilumfang(double d, double h)
    {
        if (h <= 0 || d <= 0) return 0;
        if (h >= d) return Math.PI * d;
        double r = d / 2;
        double alpha = 2 * Math.Acos((r - h) / r);
        return r * alpha;
    }

    /// <summary>Wasserspiegelbreite [m].</summary>
    public static double KreisWasserspiegelbreite(double d, double h)
    {
        if (h <= 0 || d <= 0) return 0;
        if (h >= d) return 0;
        double r = d / 2;
        return 2 * Math.Sqrt(r * r - (r - h) * (r - h));
    }

    // ── Ablagerungsgefahr nach Macke (1982) / DWA-A 110 ─────────

    /// <summary>Kritische Geschwindigkeit (iterativ).</summary>
    public static double VKrit(double cT, double AT, double vT, double kb, double rhy, double ny)
    {
        double lambda = Lambda(kb, rhy, ReynoldsZahl(vT, rhy, ny));
        if (AT == 0 || lambda == 0) return 0;

        double vcNeu = 2.19137 * Math.Pow(cT, 0.2) * Math.Pow(AT, 0.2)
                       * Math.Pow(WC, 0.3) / Math.Pow(lambda, 0.6);
        double vc;
        int iter = 0;
        do
        {
            vc = vcNeu;
            lambda = Lambda(kb, rhy, ReynoldsZahl(vc, rhy, ny));
            vcNeu = 2.19137 * Math.Pow(cT, 0.2) * Math.Pow(AT, 0.2)
                    * Math.Pow(WC, 0.3) / Math.Pow(lambda, 0.6);
            iter++;
        } while (Math.Abs(vc - vcNeu) / vcNeu > 0.001 && iter < 100);

        return vcNeu;
    }

    /// <summary>Kritische Schubspannung τ_c.</summary>
    public static double TauKrit(double cT, double Qc) =>
        462.14 * Math.Pow(Qc * cT, 1.0 / 3.0) * Math.Pow(WC, 0.5);

    /// <summary>Ablagerungsberechnung.</summary>
    public static AblagerungResult Ablagerungsgefahr(string typ, double AT, double vT, double kb, double rhy, double ny)
    {
        double cT = typ == "MR" ? CT_MR : CT_S;
        double vc = VKrit(cT, AT, vT, kb, rhy, ny);
        if (vc == 0) return new(0, 0, 0);

        double tauc = TauKrit(cT, vc * AT);
        double lambda = Lambda(kb, rhy, ReynoldsZahl(vc, rhy, ny));
        double Ic = Energiehoehengefaelle(lambda, rhy, vc);

        if (tauc < 1)
        {
            Ic /= tauc;
            double vcNew = Geschwindigkeit(Ic, rhy, kb, ny);
            return new(Ic, vcNew, 1);
        }

        return new(Ic, vc, tauc);
    }

    /// <summary>Kinematische Viskosität ν nach Temperatur [m²/s].</summary>
    public static double KinematischeViskositaet(double tempC) =>
        (1.792 - 0.0535 * tempC + 0.00065 * tempC * tempC) * 1e-6;

    /// <summary>Komplett-Berechnung für Kreisprofil.</summary>
    public static HydraulikResult? Berechne(HydraulikInput input)
    {
        double d = input.DN_mm / 1000.0;
        double hT = input.Wasserstand_mm / 1000.0;
        double IE = input.Gefaelle_Promille / 1000.0;
        double kb = input.Kb;
        double ny = KinematischeViskositaet(input.Temperatur_C);
        const double rho = 1000;

        if (d <= 0 || hT <= 0 || IE <= 0) return null;

        // Vollfüllung
        double AV = Math.PI * d * d / 4;
        double luV = Math.PI * d;
        double rhyV = AV / luV;
        double vV = Geschwindigkeit(IE, rhyV, kb, ny);
        double QV = vV * AV;

        // Teilfüllung
        double AT = KreisTeilflaeche(d, hT);
        double luT = KreisTeilumfang(d, hT);
        double bsp = KreisWasserspiegelbreite(d, hT);
        if (luT == 0) return null;
        double rhyT = AT / luT;
        double vT = Geschwindigkeit(IE, rhyT, kb, ny);
        double QT = vT * AT;

        // Kennzahlen
        double Re = ReynoldsZahl(vT, rhyT, ny);
        double Fr = FroudeZahl(vT, AT, bsp);
        double lambda = Lambda(kb, rhyT, Re);
        double tau = Schubspannung(rho, rhyT, IE);

        // Ablagerungsgefahr
        var abl = Ablagerungsgefahr(input.AbwasserTyp, AT, vT, kb, rhyT, ny);

        double auslastung = hT / d;

        return new HydraulikResult(
            // Vollfüllung
            AV, vV, QV, rhyV,
            // Teilfüllung
            AT, luT, rhyT, vT, QT, bsp,
            // Kennzahlen
            Re, Fr, lambda, tau, kb, ny,
            // Ablagerung
            abl,
            auslastung);
    }
}

public readonly record struct AblagerungResult(double Ic, double Vc, double TauC);

public sealed record HydraulikInput(
    double DN_mm,
    double Wasserstand_mm,
    double Gefaelle_Promille,
    double Kb,
    string AbwasserTyp,
    double Temperatur_C);

public sealed record HydraulikResult(
    // Vollfüllung
    double A_V, double V_V, double Q_V, double Rhy_V,
    // Teilfüllung
    double A_T, double Lu_T, double Rhy_T, double V_T, double Q_T, double Bsp,
    // Kennzahlen
    double Re, double Fr, double Lambda, double Tau, double Kb, double Ny,
    // Ablagerung
    AblagerungResult Abl,
    double Auslastung)
{
    public bool VelocityOk => V_T >= 0.5;
    public bool ShearOk => Tau >= 2.5;
    public bool AblagerungOk => V_T >= Abl.Vc;
}
