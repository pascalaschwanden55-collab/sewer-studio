using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private readonly IReadOnlyDictionary<Type, object> _services;

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (_services.TryGetValue(serviceType, out var service))
            return service;

        throw new InvalidOperationException(
            $"Kein Dienst fuer den Typ '{serviceType.FullName}' registriert.");
    }
}
