using System;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Das WebGIS hat einen Fehler gemeldet, der NICHT die Sitzung betrifft (faultstring ohne
/// Token-/Anmeldebezug, z.B. «Could not load form's default form.»). Er gilt fuer den einen
/// Aufruf: Das betroffene Objekt oder Feld wird gemeldet, der Lauf geht weiter — anders als
/// bei <see cref="WebGisSitzungException"/>, die den ganzen Lauf beendet.
/// </summary>
public sealed class WebGisAntwortException : Exception
{
    public WebGisAntwortException(string message) : base(message) { }
}
