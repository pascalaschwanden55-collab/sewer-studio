using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingSchemaTypeStateController
{
    public SchemaType? ActiveSchemaType { get; private set; }

    public void Set(SchemaType? activeSchemaType)
        => ActiveSchemaType = activeSchemaType;

    public void Clear()
        => ActiveSchemaType = null;
}
