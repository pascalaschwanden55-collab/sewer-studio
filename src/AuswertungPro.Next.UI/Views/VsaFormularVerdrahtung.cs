using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views;

/// <summary>
/// Gemeinsame Verdrahtung des VSA-Formulars in Eintrags-Editor (<c>ProtocolEntryEditorDialog</c>) und
/// Beobachtungs-Katalog (<c>ObservationCatalogWindow</c>): Live-Validierung bei Aenderung und
/// Normalisieren der strengen Eingaben beim Verlassen des Feldes. Die Regeln stehen in
/// <see cref="ProtocolEntryInputNormalizer"/>; hier steht nur die gemeinsame Verkabelung
/// (Deepscan 02.10.2026, B6).
/// </summary>
internal static class VsaFormularVerdrahtung
{
    /// <summary>Die Eingabefelder des VSA-Formulars, auf die die Verdrahtung wirkt.</summary>
    internal sealed record Felder(
        TextBox Distanz,
        TextBox Video,
        TextBox Q1,
        TextBox Q2,
        TextBox Strecke,
        TextBox Ansicht,
        TextBox Schachtbereich,
        TextBox Anmerkung,
        ComboBox UhrVon,
        ComboBox UhrBis,
        ComboBox Ez,
        CheckBox Verbindung);

    /// <summary>
    /// Haengt Validierung (bei jeder Aenderung) und Normalisierung (beim Verlassen) an die Felder.
    /// <paramref name="validiere"/> wird nach jeder Aenderung und nach jedem Normalisieren gerufen.
    /// </summary>
    internal static void Verdrahte(Felder f, Action validiere)
    {
        f.Distanz.TextChanged += (_, _) => validiere();
        f.Video.TextChanged += (_, _) => validiere();
        f.Q1.TextChanged += (_, _) => validiere();
        f.Q2.TextChanged += (_, _) => validiere();
        f.Strecke.TextChanged += (_, _) => validiere();
        f.Ansicht.TextChanged += (_, _) => validiere();
        f.Schachtbereich.TextChanged += (_, _) => validiere();
        f.Anmerkung.TextChanged += (_, _) => validiere();

        f.UhrVon.SelectionChanged += (_, _) => validiere();
        f.UhrBis.SelectionChanged += (_, _) => validiere();
        f.Ez.SelectionChanged += (_, _) => validiere();
        f.Verbindung.Checked += (_, _) => validiere();
        f.Verbindung.Unchecked += (_, _) => validiere();

        f.Distanz.LostFocus += (_, _) => Zahl(f.Distanz, validiere);
        f.Video.LostFocus += (_, _) => Zeit(f.Video, validiere);
        f.Q1.LostFocus += (_, _) => Zahl(f.Q1, validiere);
        f.Q2.LostFocus += (_, _) => Zahl(f.Q2, validiere);
        f.Strecke.LostFocus += (_, _) => Strecke(f.Strecke, validiere);
        f.UhrVon.LostFocus += (_, _) => Uhrlage(f.UhrVon, validiere);
        f.UhrBis.LostFocus += (_, _) => Uhrlage(f.UhrBis, validiere);
        f.Ez.LostFocus += (_, _) => Ez(f.Ez, validiere);
        f.Schachtbereich.LostFocus += (_, _) => Schachtbereich(f.Schachtbereich, validiere);
    }

    // Die Normalisierer: Ist der Text nicht lesbar, bleibt er stehen (nur validieren, der Fehler wird
    // dort gezeigt); sonst ersetzt der normalisierte Text die Eingabe. validiere == null: nicht validieren.

    internal static void Zahl(TextBox box, Action? validiere)
    {
        if (ProtocolEntryInputNormalizer.TryParseOptionalDouble(box.Text, out var value))
            SetzeText(box, value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty);
        validiere?.Invoke();
    }

    internal static void Zeit(TextBox box, Action? validiere)
    {
        if (ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(box.Text, out var value))
            SetzeText(box, value.HasValue ? ProtocolEntryInputNormalizer.FormatTime(value.Value) : string.Empty);
        validiere?.Invoke();
    }

    internal static void Strecke(TextBox box, Action? validiere)
    {
        if (ProtocolEntryInputNormalizer.TryNormalizeStrecke(box.Text, out var normalized, out _))
            SetzeText(box, normalized);
        validiere?.Invoke();
    }

    internal static void Uhrlage(ComboBox box, Action? validiere)
    {
        if (ProtocolEntryInputNormalizer.TryNormalizeClockPosition(box.Text, out var normalized, out _))
            SetzeText(box, normalized);
        validiere?.Invoke();
    }

    internal static void Ez(ComboBox box, Action? validiere)
    {
        if (ProtocolEntryInputNormalizer.TryNormalizeEz(box.Text, out var normalized, out _))
            SetzeText(box, normalized);
        validiere?.Invoke();
    }

    internal static void Schachtbereich(TextBox box, Action? validiere)
    {
        if (ProtocolEntryInputNormalizer.TryNormalizeSchachtbereich(box.Text, out var normalized, out _))
            SetzeText(box, normalized);
        validiere?.Invoke();
    }

    private static void SetzeText(TextBox box, string normalized)
    {
        if (!string.Equals(box.Text, normalized, StringComparison.Ordinal))
            box.Text = normalized;
    }

    private static void SetzeText(ComboBox box, string normalized)
    {
        if (!string.Equals(box.Text, normalized, StringComparison.Ordinal))
            box.Text = normalized;
    }
}
