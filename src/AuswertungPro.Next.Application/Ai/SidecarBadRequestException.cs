using System;
using System.Net;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Fachliche Ausnahme fuer 4xx-Antworten des Vision-Sidecars.
/// </summary>
public sealed class SidecarBadRequestException : Exception
{
    public SidecarBadRequestException(string endpoint, HttpStatusCode statusCode, string responseBody)
        : base($"Vision-Sidecar {endpoint} meldet HTTP {(int)statusCode}: {responseBody}")
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string Endpoint { get; }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }
}
