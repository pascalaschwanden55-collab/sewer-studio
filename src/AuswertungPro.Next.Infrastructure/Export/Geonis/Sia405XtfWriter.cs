using System.Text;
using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export.Geonis;

namespace AuswertungPro.Next.Infrastructure.Export.Geonis;

/// <summary>
/// Schreibt die SIA405-Transferdatei fuer den GEONIS-Rueckschrieb.
///
/// Kopf und Modellangaben stammen unveraendert aus der Kataster-Quelldatei; die Objekte sind die
/// Originalobjekte mit ausgetauschten Werten. Dadurch bleibt die Datei im selben Modell gueltig
/// und traegt die OBJ_ID des Katasters als Schluessel.
/// </summary>
public sealed class Sia405XtfWriter : ISia405XtfWriter
{
    public void Schreibe(Sia405ExportPlan plan, IReadOnlyDictionary<string, string> quelltexteNachTid, string zielPfad)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(quelltexteNachTid);

        if (string.IsNullOrWhiteSpace(zielPfad))
            throw new ArgumentException("Zielpfad fehlt.", nameof(zielPfad));
        if (plan.Objekte.Count == 0)
            throw new InvalidOperationException("Der Plan enthaelt keine Objekte. Es wird keine leere Transferdatei geschrieben.");
        if (string.IsNullOrWhiteSpace(plan.Modell.TopicPrefix))
            throw new InvalidOperationException("Die Kataster-Quelldatei hat keinen lesbaren Modellpraefix geliefert.");

        var ns = XNamespace.Get(plan.Modell.TransferNamespace);

        var behaelter = new XElement(
            ns + plan.Modell.TopicPrefix,
            new XAttribute("BID", string.IsNullOrWhiteSpace(plan.Modell.BasketId)
                ? "x" + Guid.NewGuid().ToString("N")
                : plan.Modell.BasketId!));

        foreach (var objekt in plan.Objekte)
        {
            if (!quelltexteNachTid.TryGetValue(objekt.Tid, out var quelltext) || string.IsNullOrWhiteSpace(quelltext))
            {
                throw new InvalidOperationException(
                    $"Fuer {objekt.Klasse} '{objekt.Bezeichnung}' fehlt der Quelltext aus dem Kataster (TID {objekt.Tid}).");
            }

            var element = XElement.Parse(quelltext, LoadOptions.None);
            foreach (var aenderung in objekt.Aenderungen)
                SetzeWert(element, objekt.Klasse, aenderung.Attribut, aenderung.Neu, plan.AttributReihenfolge);

            behaelter.Add(element);
        }

        var modelle = plan.Modell.Modelle
            .Select(m => new XElement(
                ns + "MODEL",
                new XAttribute("NAME", m.Name),
                new XAttribute("VERSION", m.Version),
                new XAttribute("URI", m.Uri)))
            .ToList();

        var transfer = new XElement(
            ns + "TRANSFER",
            new XElement(
                ns + "HEADERSECTION",
                new XAttribute("SENDER", "SewerStudio"),
                new XAttribute("VERSION", plan.Modell.HeaderVersion),
                new XElement(ns + "MODELS", modelle)),
            new XElement(ns + "DATASECTION", behaelter));

        var dokument = new XDocument(new XDeclaration("1.0", "UTF-8", null), transfer);

        var ordner = Path.GetDirectoryName(zielPfad);
        if (!string.IsNullOrEmpty(ordner))
            Directory.CreateDirectory(ordner);

        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  " };
        AtomicTextFileWriter.Write(
            zielPfad,
            textWriter =>
            {
                using var xmlWriter = XmlWriter.Create(textWriter, settings);
                dokument.Save(xmlWriter);
            },
            new UTF8Encoding(false));
    }

    /// <summary>
    /// Setzt einen Attributwert. Fehlt das Element noch (typisch bei Baulicher_Zustand oder
    /// Bemerkung), wird es an der Stelle eingefuegt, die die Reihenfolge im Kataster vorgibt.
    /// </summary>
    private static void SetzeWert(
        XElement objekt,
        string klasse,
        string attribut,
        string wert,
        Sia405AttributReihenfolge reihenfolge)
    {
        var vorhanden = objekt.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, attribut, StringComparison.Ordinal));
        if (vorhanden is not null)
        {
            vorhanden.Value = wert;
            return;
        }

        var namen = objekt.Elements().Select(e => e.Name.LocalName).ToList();
        var index = reihenfolge.IndexFuerEinfuegen(klasse, namen, attribut);
        var neu = new XElement(objekt.Name.Namespace + attribut, wert);

        if (index >= namen.Count)
        {
            objekt.Add(neu);
            return;
        }

        objekt.Elements().ElementAt(index).AddBeforeSelf(neu);
    }
}
