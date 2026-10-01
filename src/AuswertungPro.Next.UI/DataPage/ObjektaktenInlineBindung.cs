using System;
using System.Windows;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Controls;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Hält genau die Akte der offenen Zeile; Suche und Objektauswahl bleiben beim Nachziehen erhalten.</summary>
internal sealed class ObjektaktenInlineBindung
{
    private object? _datensatz;
    private object? _kontext;
    private ObjektakteViewModel? _model;

    public ObjektakteViewModel? Aktualisiere(FrameworkElement bereich, object datensatz, object? kontext,
        Func<ObjektakteViewModel?> erstellen)
    {
        if (ReferenceEquals(datensatz, _datensatz) && ReferenceEquals(kontext, _kontext)) return _model;
        Leere(bereich);
        _model = erstellen();
        _datensatz = datensatz;
        _kontext = kontext;
        return _model;
    }

    public void Leere(FrameworkElement bereich)
    {
        ObjektakteView.UebernehmeEingabe(bereich);
        _model?.SpeichereAnsicht();
        _model = null;
        _datensatz = null;
        _kontext = null;
    }
}
