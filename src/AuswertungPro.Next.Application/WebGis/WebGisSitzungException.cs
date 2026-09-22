using System;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Die WebOffice-Sitzung ist abgelaufen oder ungueltig — neu anmelden, nicht weiterlesen.</summary>
public sealed class WebGisSitzungException : Exception
{
    public WebGisSitzungException(string message) : base(message) { }
}
